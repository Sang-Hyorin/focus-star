import Foundation
import CryptoKit

let sampleHeader = "timestamp,state,reason,idle_seconds,foreground_process,sample_seconds,platform,device_id,start_epoch,end_epoch\n"
enum SyncFailure: Error { case invalidCSV(String), changed(String), verification(String) }

// Refuse to overwrite incomplete / unrecognized cloud files during a WPS transfer.
func sampleRows(_ data: Data, path: String) throws -> [[String]] {
    guard let text = String(data:data,encoding:.utf8), text.hasSuffix("\n") else { throw SyncFailure.invalidCSV(path) }
    let rows = csv(text)
    guard rows.first == csv(sampleHeader).first else { throw SyncFailure.invalidCSV(path) }
    for row in rows.dropFirst() {
        guard row.count == 10,
              let seconds = Double(row[5]), seconds.isFinite, seconds > 0, seconds <= 3600,
              let start = Double(row[8]), start.isFinite, let end = Double(row[9]), end.isFinite,
              end > start, abs(end-start-seconds) < 0.1 else { throw SyncFailure.invalidCSV(path) }
    }
    return Array(rows.dropFirst())
}
func encodeSamples(_ rows: [[String]]) -> Data {
    Data((sampleHeader + rows.map { $0.map(cell).joined(separator:",") + "\n" }.joined()).utf8)
}
func writeChanged(_ data: Data, to url: URL) throws {
    if FileManager.default.fileExists(atPath:url.path), try Data(contentsOf:url) == data { return }
    try FileManager.default.createDirectory(at:url.deletingLastPathComponent(),withIntermediateDirectories:true)
    try data.write(to:url,options:.atomic)
    let handle = try FileHandle(forWritingTo:url)
    defer { try? handle.close() }
    try handle.synchronize()
    guard try Data(contentsOf:url) == data else { throw SyncFailure.verification(url.path) }
}

final class LocalSync {
    let root: URL, local: URL, device: String
    init(root: URL, device: String, base: URL? = nil) {
        self.root = root; self.device = device
        let support = base ?? FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/FocusStarMac")
        let key = SHA256.hash(data:Data(root.standardizedFileURL.path.utf8)).map { String(format:"%02x",$0) }.joined()
        local = support.appendingPathComponent("outbox").appendingPathComponent(key).appendingPathComponent(device)
    }
    func files() throws -> [URL] {
        guard FileManager.default.fileExists(atPath:local.path) else { return [] }
        return try FileManager.default.contentsOfDirectory(at:local,includingPropertiesForKeys:nil)
            .filter { $0.pathExtension == "csv" }.sorted { $0.path < $1.path }
    }
    func save(row: [String], day: String) throws {
        let url = local.appendingPathComponent(day + ".csv")
        var rows: [[String]] = []
        if FileManager.default.fileExists(atPath:url.path) { rows = try sampleRows(Data(contentsOf:url),path:url.path) }
        if !rows.contains(row) { rows.append(row) }
        let data = encodeSamples(rows)
        _ = try sampleRows(data,path:url.path)
        try writeChanged(data,to:url)
    }
    func publish() throws {
        for url in try files() {
            let snapshot = try Data(contentsOf:url)
            let pending = try sampleRows(snapshot,path:url.path)
            let target = root.appendingPathComponent("mac-data").appendingPathComponent(device).appendingPathComponent("v2").appendingPathComponent(url.lastPathComponent)
            let exists = FileManager.default.fileExists(atPath:target.path)
            let previous = exists ? try Data(contentsOf:target) : nil
            var rows = try previous.map { try sampleRows($0,path:target.path) } ?? []
            var seen = Set(rows)
            for row in pending where seen.insert(row).inserted { rows.append(row) }
            rows.sort { (Double($0[9]) ?? 0) < (Double($1[9]) ?? 0) }
            let output = encodeSamples(rows)
            // Check again immediately before replacing a file which WPS may update.
            if let previous = previous {
                guard try Data(contentsOf:target) == previous else { throw SyncFailure.changed(target.path) }
            } else if FileManager.default.fileExists(atPath:target.path) { throw SyncFailure.changed(target.path) }
            try writeChanged(output,to:target)
            guard try Data(contentsOf:target) == output else { throw SyncFailure.verification(target.path) }
            guard try Data(contentsOf:url) == snapshot else { throw SyncFailure.changed(url.path) }
            // Crash before this removal is safe: the next publish deduplicates exact rows.
            try FileManager.default.removeItem(at:url)
        }
    }
}

struct SyncSchedule {
    var lastAttempt = Date.distantPast
    mutating func begin(now: Date = Date(), force: Bool = false) -> Bool {
        guard force || now.timeIntervalSince(lastAttempt) >= 600 else { return false }
        lastAttempt = now
        return true
    }
}
