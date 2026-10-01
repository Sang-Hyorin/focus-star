import Foundation

private let archiveHeader = "timestamp,state,reason,idle_seconds,foreground_process,sample_seconds,platform,device_id,start_epoch,end_epoch"

private struct MacArchiveSpan {
    let start: Date
    let end: Date
    let app: String
}

private struct MacArchiveSnapshot {
    let url: URL
    let data: Data
}

private func exactArchiveDate(_ url: URL) -> Date? {
    guard url.pathExtension.caseInsensitiveCompare("csv") == .orderedSame else { return nil }
    let name = url.deletingPathExtension().lastPathComponent
    guard name.range(of: "^\\d{4}-\\d{2}-\\d{2}$", options: .regularExpression) != nil else { return nil }
    let formatter = DateFormatter()
    formatter.timeZone = focusTimeZone
    formatter.locale = Locale(identifier: "en_US_POSIX")
    formatter.dateFormat = "yyyy-MM-dd"
    formatter.isLenient = false
    return formatter.date(from: name)
}

private func parseArchiveData(_ data: Data, device: String, history: Bool) -> (spans: [MacArchiveSpan]?, error: String?) {
    guard let raw = String(data: data, encoding: .utf8) else { return (nil,"invalid UTF-8") }
    guard raw.hasSuffix("\n") else { return (nil,"incomplete trailing row") }
    let rows = csv(raw)
    guard let header = rows.first else { return (nil,"missing header") }
    func index(_ name: String) -> Int? { header.firstIndex(of: name) }
    guard let timestamp = index("timestamp"), let state = index("state"), let app = index("foreground_process"), let duration = index("sample_seconds") else { return (nil,"unsupported header") }
    let platform = index("platform"), deviceIndex = index("device_id"), startEpoch = index("start_epoch"), endEpoch = index("end_epoch")
    if history && (platform == nil || deviceIndex == nil || startEpoch == nil || endEpoch == nil) { return (nil,"unsupported history header") }
    let formatter = DateFormatter()
    formatter.timeZone = focusTimeZone
    formatter.locale = Locale(identifier: "en_US_POSIX")
    formatter.dateFormat = "yyyy-MM-dd HH:mm:ss"
    formatter.isLenient = false
    var work: [MacArchiveSpan] = []
    for (number,row) in rows.dropFirst().enumerated() {
        guard row.count == header.count, let seconds = Double(row[duration]), seconds.isFinite, seconds > 0, seconds <= 3600 else { return (nil,"invalid row \(number+1)") }
        if let i = platform, !row[i].isEmpty && row[i].caseInsensitiveCompare("macOS") != .orderedSame { return (nil,"contains non-Mac data") }
        if let i = deviceIndex, !row[i].isEmpty && row[i].caseInsensitiveCompare(device) != .orderedSame { return (nil,"contains another Mac device") }
        let end: Date
        if let i = endEpoch, let value = Double(row[i]), value.isFinite { end = Date(timeIntervalSince1970: value) }
        else if let parsed = formatter.date(from: row[timestamp]) { end = parsed }
        else { return (nil,"invalid timestamp row \(number+1)") }
        let start: Date
        if let i = startEpoch, let value = Double(row[i]), value.isFinite { start = Date(timeIntervalSince1970: value) }
        else { start = end.addingTimeInterval(-seconds) }
        let actual = end.timeIntervalSince(start)
        guard actual > 0, actual <= 3600, abs(actual-seconds) < 0.1 else { return (nil,"invalid interval row \(number+1)") }
        if row[state] == "Work" { work.append(MacArchiveSpan(start:start,end:end,app:row[app])) }
        else if history { return (nil,"history contains non-Work row \(number+1)") }
    }
    return (work,nil)
}

private func canonicalMacArchive(_ spans: [MacArchiveSpan]) -> [MacArchiveSpan] {
    let calendar = focusCalendar
    var groups: [String: [MacArchiveSpan]] = [:]
    for span in spans where span.end > span.start {
        var cursor = span.start
        while cursor < span.end {
            guard let boundary = calendar.date(byAdding: .day,value:1,to:calendar.startOfDay(for:cursor)) else { break }
            let stop = min(span.end,boundary)
            let key = dateString(cursor,"yyyy-MM-dd") + "\u{0}" + span.app.lowercased()
            groups[key,default:[]].append(MacArchiveSpan(start:cursor,end:stop,app:span.app))
            cursor = stop
        }
    }
    var result: [MacArchiveSpan] = []
    for group in groups.values {
        let sorted = group.sorted { $0.start < $1.start }
        guard let first = sorted.first else { continue }
        var start = first.start, end = first.end, app = first.app
        func appendBounded(_ from: Date, _ to: Date) {
            var cursor = from
            while cursor < to {
                let stop = min(to,cursor.addingTimeInterval(3600))
                result.append(MacArchiveSpan(start:cursor,end:stop,app:app)); cursor = stop
            }
        }
        for span in sorted.dropFirst() {
            if span.start <= end { end = max(end,span.end) }
            else { appendBounded(start,end); start=span.start; end=span.end; app=span.app }
        }
        appendBounded(start,end)
    }
    return result.sorted { $0.start == $1.start ? $0.app.localizedCaseInsensitiveCompare($1.app) == .orderedAscending : $0.start < $1.start }
}

private func serializeMacArchive(_ spans: [MacArchiveSpan], device: String) -> String {
    var lines = [archiveHeader]
    for span in spans {
        let values = [dateString(span.end,"yyyy-MM-dd HH:mm:ss"),"Work","Archived","0",span.app,String(format:"%.3f",span.end.timeIntervalSince(span.start)),"macOS",device,String(format:"%.3f",span.start.timeIntervalSince1970),String(format:"%.3f",span.end.timeIntervalSince1970)]
        lines.append(values.map(cell).joined(separator:","))
    }
    return lines.joined(separator:"\n") + "\n"
}

private func archiveSignature(_ spans: [MacArchiveSpan]) -> String {
    canonicalMacArchive(spans).map { String(format:"%.3f|%.3f|%@",$0.start.timeIntervalSince1970,$0.end.timeIntervalSince1970,$0.app.lowercased()) }.joined(separator:"\n")
}

func archivePastMac(root: URL, device: String) -> [String] {
    guard !device.isEmpty else { return ["device ID unavailable"] }
    let fm = FileManager.default
    let base = root.appendingPathComponent("mac-data").appendingPathComponent(device)
    let today = focusCalendar.startOfDay(for:Date())
    var warnings: [String] = [], snapshots: [MacArchiveSnapshot] = [], all: [MacArchiveSpan] = []
    if let enumerator = fm.enumerator(at:base,includingPropertiesForKeys:nil) {
        for case let url as URL in enumerator {
            guard let day = exactArchiveDate(url), day < today else { continue }
            do {
                let data = try Data(contentsOf:url)
                let parsed = parseArchiveData(data,device:device,history:false)
                guard let spans = parsed.spans else { warnings.append("\(url.lastPathComponent) retained: \(parsed.error ?? "invalid")"); continue }
                snapshots.append(MacArchiveSnapshot(url:url,data:data)); all.append(contentsOf:spans)
            } catch { warnings.append("\(url.lastPathComponent) retained: \(error.localizedDescription)") }
        }
    }
    guard !snapshots.isEmpty else { return warnings }
    let history = base.appendingPathComponent("history.csv")
    var prior: MacArchiveSnapshot?
    do {
        if fm.fileExists(atPath:history.path) {
            let data = try Data(contentsOf:history), parsed = parseArchiveData(data,device:device,history:true)
            guard let spans = parsed.spans else { warnings.append("history.csv unreadable; details retained: \(parsed.error ?? "invalid")"); return warnings }
            prior = MacArchiveSnapshot(url:history,data:data); all.append(contentsOf:spans)
        }
        let canonical = canonicalMacArchive(all), content = Data(serializeMacArchive(canonical,device:device).utf8)
        try fm.createDirectory(at:base,withIntermediateDirectories:true)
        let temporary = base.appendingPathComponent("history.\(UUID().uuidString).partial")
        defer { try? fm.removeItem(at:temporary) }
        guard fm.createFile(atPath:temporary.path,contents:nil) else { throw NSError(domain:"FocusStarArchive",code:1,userInfo:[NSLocalizedDescriptionKey:"cannot create temporary history"]) }
        let handle = try FileHandle(forWritingTo:temporary)
        try handle.write(contentsOf:content); try handle.synchronize(); try handle.close()
        let verified = parseArchiveData(try Data(contentsOf:temporary),device:device,history:true)
        guard let verifiedSpans = verified.spans, archiveSignature(verifiedSpans) == archiveSignature(canonical) else { throw NSError(domain:"FocusStarArchive",code:2,userInfo:[NSLocalizedDescriptionKey:"archive verification failed"]) }
        guard snapshots.allSatisfy({ snapshot in
            guard let current = try? Data(contentsOf:snapshot.url) else { return false }
            return current == snapshot.data
        }) else { throw NSError(domain:"FocusStarArchive",code:3,userInfo:[NSLocalizedDescriptionKey:"source changed during archive"]) }
        if let old = prior {
            guard let current = try? Data(contentsOf:history), current == old.data else { throw NSError(domain:"FocusStarArchive",code:4,userInfo:[NSLocalizedDescriptionKey:"history changed during archive"]) }
        }
        else if fm.fileExists(atPath:history.path) { throw NSError(domain:"FocusStarArchive",code:5,userInfo:[NSLocalizedDescriptionKey:"history appeared during archive"]) }
        if fm.fileExists(atPath:history.path) { _ = try fm.replaceItemAt(history,withItemAt:temporary) }
        else { try fm.moveItem(at:temporary,to:history) }
        for snapshot in snapshots {
            guard let current = try? Data(contentsOf:snapshot.url), current == snapshot.data else { warnings.append("\(snapshot.url.lastPathComponent) changed after archive; retained"); continue }
            do { try fm.removeItem(at:snapshot.url) }
            catch { warnings.append("\(snapshot.url.lastPathComponent) archived but retained: \(error.localizedDescription)") }
        }
    } catch { warnings.append("Archive deferred: \(error.localizedDescription)") }
    return warnings
}
