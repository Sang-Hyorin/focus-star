import AppKit
import SwiftUI
import CoreGraphics
import Darwin

let focusTimeZone = TimeZone(secondsFromGMT: 8 * 3600)!
var focusCalendar: Calendar {
    var calendar = Calendar(identifier: .gregorian)
    calendar.timeZone = focusTimeZone
    calendar.firstWeekday = 2
    calendar.minimumDaysInFirstWeek = 4
    return calendar
}

struct Rules: Codable {
    var dailyGoalMinutes: Double = 480
    var inputActiveSeconds: Double = 60
    var pollIntervalSeconds: Double = 30
    var workAppIdleGraceSeconds: Double = 180
    var workProcessNames: [String] = []
}
func csv(_ text: String) -> [[String]] {
    var rows = [[String]](), row = [String](), field = "", quoted = false
    let a = Array(text.replacingOccurrences(of: "\u{feff}", with: "").replacingOccurrences(of: "\r\n", with: "\n")); var i = 0
    while i < a.count {
        let c = a[i]
        if c == "\"" {
            if quoted && i+1 < a.count && a[i+1] == "\"" { field.append(c); i += 1 }
            else { quoted.toggle() }
        } else if c == "," && !quoted { row.append(field); field = "" }
        else if c == "\n" && !quoted { row.append(field); rows.append(row); row = []; field = "" }
        else if c != "\r" || quoted { field.append(c) }
        i += 1
    }
    // A partially synced trailing row is deliberately ignored.
    return rows
}
func esc(_ s: String) -> String { s.replacingOccurrences(of: "&", with: "&amp;").replacingOccurrences(of: "<", with: "&lt;").replacingOccurrences(of: ">", with: "&gt;").replacingOccurrences(of: "\"", with: "&quot;") }
func cell(_ s: String) -> String { "\"" + s.replacingOccurrences(of: "\"", with: "\"\"") + "\"" }
func duration(_ s: Double) -> String { let m = Int(s / 60); return "\(m / 60)h \(m % 60)m" }
func dateString(_ d: Date, _ pattern: String) -> String { let f = DateFormatter(); f.timeZone = focusTimeZone; f.locale = Locale(identifier: "en_US_POSIX"); f.dateFormat = pattern; return f.string(from: d) }
func isRecordCSV(_ url: URL) -> Bool {
    guard url.pathExtension.lowercased() == "csv" else { return false }
    let name = url.deletingPathExtension().lastPathComponent
    return name == "history" || name.range(of:"^\\d{4}-\\d{2}-\\d{2}",options:.regularExpression) != nil
}
let aliases: [String: String] = ["com.microsoft.VSCode":"Code", "com.openai.codex":"Codex", "com.openai.chat":"ChatGPT", "notion.id":"Notion", "com.kingsoft.wpsoffice.mac":"wps", "com.kingsoft.wpsoffice.mac.global":"wps", "com.microsoft.Word":"WINWORD", "com.microsoft.Powerpoint":"POWERPNT", "com.microsoft.Excel":"et", "com.microsoft.Outlook":"OUTLOOK", "com.github.GitHubClient":"GitHubDesktop", "io.github.clash-verge-rev.clash-verge-rev":"clash-verge"]
func isWork(_ state: String, _ process: String, _ rules: Rules) -> Bool {
    state == "Work" && rules.workProcessNames.contains { $0.caseInsensitiveCompare(process) == .orderedSame }
}
struct FocusInterval {
    let start: Date
    let end: Date
}
func unionSeconds(_ intervals: [FocusInterval]) -> Double {
    let sorted = intervals.filter { $0.end > $0.start }.sorted { $0.start < $1.start }
    guard let first = sorted.first else { return 0 }
    var start = first.start, end = first.end, seconds: Double = 0
    for interval in sorted.dropFirst() {
        if interval.start <= end { end = max(end, interval.end) }
        else { seconds += end.timeIntervalSince(start); start = interval.start; end = interval.end }
    }
    return seconds + end.timeIntervalSince(start)
}
// History stores merged Work intervals per process, retaining cross-device overlap bounds.
func mergedIntervals(_ intervals: [FocusInterval]) -> [FocusInterval] {
    var result: [FocusInterval] = []
    for i in intervals.sorted(by: { $0.start < $1.start }) where i.end > i.start {
        if let last = result.last, i.start <= last.end {
            result[result.count-1] = FocusInterval(start: last.start, end: max(last.end,i.end))
        } else { result.append(i) }
    }
    return result
}
enum ArchiveFailure: LocalizedError {
    case invalidFile(String)
    var errorDescription: String? {
        switch self {
        case .invalidFile(let path): return "Invalid archive input: \(path)"
        }
    }
}
func archiveHistory(_ root: URL, device: String, now: Date = Date()) throws {
    let fm = FileManager.default
    let dir = root.appendingPathComponent("mac-data").appendingPathComponent(device)
    guard fm.fileExists(atPath: dir.path) else { return }
    let history = dir.appendingPathComponent("history.csv")
    let today = dateString(now,"yyyy-MM-dd")
    let cutoff = focusCalendar.startOfDay(for:now)
    var snapshots: [(URL,Data)] = []
    if let e = fm.enumerator(at:dir,includingPropertiesForKeys:nil) {
        for case let url as URL in e {
            let name = url.deletingPathExtension().lastPathComponent
            if url.pathExtension == "csv", name.range(of:"^\\d{4}-\\d{2}-\\d{2}$",options:.regularExpression) != nil, name < today {
                snapshots.append((url,try Data(contentsOf:url)))
            }
        }
    }
    guard !snapshots.isEmpty else { return }
    var input = snapshots
    if fm.fileExists(atPath:history.path) { input.append((history,try Data(contentsOf:history))) }
    var groups: [String:[FocusInterval]] = [:]
    let f = DateFormatter(); f.timeZone = focusTimeZone; f.locale = Locale(identifier:"en_US_POSIX"); f.dateFormat = "yyyy-MM-dd HH:mm:ss"; f.isLenient = false
    var deferred = Set<URL>()
    for (url,data) in input {
        guard let text = String(data:data,encoding:.utf8), text.hasSuffix("\n"),
              let h = csv(text).first,
              let ts = h.firstIndex(of:"timestamp"), let st = h.firstIndex(of:"state"),
              let ap = h.firstIndex(of:"foreground_process"), let se = h.firstIndex(of:"sample_seconds") else { throw ArchiveFailure.invalidFile(url.path) }
        for r in csv(text).dropFirst() {
            guard r.count == h.count, let secs = Double(r[se]), secs.isFinite, secs > 0, secs <= 3600 else { throw ArchiveFailure.invalidFile(url.path) }
            let end = h.firstIndex(of:"end_epoch").flatMap { Double(r[$0]) }.map { Date(timeIntervalSince1970:$0) } ?? f.date(from:r[ts])
            guard let end = end, end.timeIntervalSince1970.isFinite else { throw ArchiveFailure.invalidFile(url.path) }
            let start = h.firstIndex(of:"start_epoch").flatMap { Double(r[$0]) }.map { Date(timeIntervalSince1970:$0) } ?? end.addingTimeInterval(-secs)
            guard start.timeIntervalSince1970.isFinite, end > start, abs(end.timeIntervalSince(start)-secs) < 0.1 else { throw ArchiveFailure.invalidFile(url.path) }
            if end > cutoff {
                // The filename may use an older system timezone. Keep the whole
                // file until every record is before UTC+08:00 midnight.
                deferred.insert(url)
                continue
            }
            if r[st] == "Work" { groups[r[ap],default:[]].append(FocusInterval(start:start,end:end)) }
        }
    }
    snapshots.removeAll { deferred.contains($0.0) }
    guard !snapshots.isEmpty else { return }
    var output = "timestamp,state,reason,idle_seconds,foreground_process,sample_seconds,platform,device_id,start_epoch,end_epoch\n"
    for app in groups.keys.sorted() {
        for interval in mergedIntervals(groups[app]!) {
            var cursor = interval.start
            while cursor < interval.end {
                let midnight = focusCalendar.date(byAdding:.day,value:1,to:focusCalendar.startOfDay(for:cursor))!
                let end = min(interval.end,min(midnight,cursor.addingTimeInterval(3600)))
                output += [dateString(end,"yyyy-MM-dd HH:mm:ss"),"Work","Archived", "0",app,String(format:"%.3f",end.timeIntervalSince(cursor)),"macOS",device,String(format:"%.3f",cursor.timeIntervalSince1970),String(format:"%.3f",end.timeIntervalSince1970)].map(cell).joined(separator:",") + "\n"
                cursor = end
            }
        }
    }
    let data = Data(output.utf8)
    try data.write(to:history,options:.atomic)
    guard try Data(contentsOf:history) == data else { throw ArchiveFailure.invalidFile(history.path) }
    // Never remove a file that changed during archiving. Readers union raw + history
    // so an interrupted cleanup or late cloud reappearance cannot double-count.
    for (url,snapshot) in snapshots where (try? Data(contentsOf:url)) == snapshot { try fm.removeItem(at:url) }
}
struct Totals {
    var windows: Double = 0, mac: Double = 0, focus: Double = 0
    var intervals: [FocusInterval] = []
    var apps: [String: Double] = [:]
    mutating func add(_ platform: String, _ seconds: Double, _ app: String) {
        if platform == "macOS" { mac += seconds } else { windows += seconds }
        apps[platform + " · " + app, default: 0] += seconds
    }
}
func mergeSavedFocusCSV(_ text: String, into values: inout [String: Double]) {
    let rows = csv(text)
    guard let header = rows.first,
          let dateColumn = header.firstIndex(of:"date"),
          let focusColumn = header.firstIndex(of:"focus_seconds") else { return }
    let timezoneColumn = header.firstIndex(of:"timezone")
    let formatter = DateFormatter()
    formatter.timeZone = focusTimeZone
    formatter.locale = Locale(identifier:"en_US_POSIX")
    formatter.calendar = Calendar(identifier:.gregorian)
    formatter.timeZone = TimeZone(secondsFromGMT:8*3600)
    formatter.dateFormat = "yyyy-MM-dd"
    formatter.isLenient = false
    for row in rows.dropFirst() {
        guard row.count == header.count,
              let date = formatter.date(from:row[dateColumn]), formatter.string(from:date) == row[dateColumn],
              let seconds = Double(row[focusColumn]), seconds.isFinite, seconds >= 0, seconds <= 86400 else { continue }
        if let timezoneColumn = timezoneColumn, !row[timezoneColumn].isEmpty,
           row[timezoneColumn].caseInsensitiveCompare("UTC+08:00") != .orderedSame { continue }
        values[row[dateColumn]] = max(values[row[dateColumn]] ?? 0,seconds)
    }
}
func loadSavedFocus(_ root: URL, localLedger: URL) -> [String: Double] {
    var values: [String: Double] = [:]
    if let text = try? String(contentsOf:localLedger,encoding:.utf8) { mergeSavedFocusCSV(text,into:&values) }
    let fm = FileManager.default
    for folder in ["windows-reports","mac-reports"] {
        guard let enumerator = fm.enumerator(at:root.appendingPathComponent(folder),includingPropertiesForKeys:nil) else { continue }
        for case let url as URL in enumerator where url.pathExtension.lowercased() == "csv" {
            if let text = try? String(contentsOf:url,encoding:.utf8) { mergeSavedFocusCSV(text,into:&values) }
        }
    }
    return values
}
func applySettledFocus(_ computed: [String: Totals], saved: inout [String: Double]) -> [String: Totals] {
    var result = computed
    for (date,total) in computed where total.focus.isFinite && total.focus >= 0 && total.focus <= 86400 {
        saved[date] = max(saved[date] ?? 0,total.focus)
    }
    for (date,seconds) in saved {
        var total = result[date] ?? Totals()
        total.focus = max(total.focus,seconds)
        result[date] = total
    }
    return result
}
func saveSettledFocus(_ values: [String: Double], to url: URL) throws {
    let rows = values.keys.sorted().map { $0 + "," + String(format:"%.3f",values[$0]!) }
    let text = "date,focus_seconds\n" + rows.joined(separator:"\n") + (rows.isEmpty ? "" : "\n")
    try writeChanged(Data(text.utf8),to:url)
}
func summarize(_ root: URL, _ rules: Rules, localFiles: [URL] = []) -> [String: Totals] {
    var result: [String: Totals] = [:]
    var grouped: [String:[String:[FocusInterval]]] = [:]
    var byPlatform: [String:[String:[FocusInterval]]] = [:]
    let fm = FileManager.default
    let legacy = (try? fm.contentsOfDirectory(at: root.appendingPathComponent("data"), includingPropertiesForKeys: nil)) ?? []
    var sources = legacy.filter { isRecordCSV($0) && $0.lastPathComponent != "history.csv" }.map { ($0, "Windows") }
    if let e = fm.enumerator(at: root.appendingPathComponent("mac-data"), includingPropertiesForKeys: nil) {
        for case let u as URL in e where isRecordCSV(u) { sources.append((u,"macOS")) }
    }
    if let e = fm.enumerator(at: root.appendingPathComponent("windows-data"), includingPropertiesForKeys: nil) {
        for case let u as URL in e where isRecordCSV(u) { sources.append((u,"Windows")) }
    }
    sources.append(contentsOf:localFiles.map { ($0,"macOS") })
    let formatter = DateFormatter()
    formatter.timeZone = focusTimeZone
    formatter.locale = Locale(identifier: "en_US_POSIX")
    formatter.dateFormat = "yyyy-MM-dd HH:mm:ss"
    formatter.isLenient = false
    let calendar = focusCalendar
    for (u, platform) in sources {
        guard let raw = try? String(contentsOf: u, encoding: .utf8) else { continue }
        let rows = csv(raw)
        guard let header = rows.first else { continue }
        let names = ["timestamp","state","foreground_process","sample_seconds"]
        let indexes = names.compactMap { header.firstIndex(of: $0) }
        guard indexes.count == 4 else { continue }
        for r in rows.dropFirst() {
            guard r.count == header.count, let secs = Double(r[indexes[3]]), secs.isFinite, secs > 0, secs <= 3600,
                  isWork(r[indexes[1]],r[indexes[2]],rules) else { continue }
            let end: Date
            if let idx = header.firstIndex(of: "end_epoch"), let epoch = Double(r[idx]), epoch.isFinite {
                end = Date(timeIntervalSince1970: epoch)
            } else if let parsed = formatter.date(from: r[indexes[0]]) { end = parsed }
            else { continue }
            let start: Date
            if let idx = header.firstIndex(of: "start_epoch"), let epoch = Double(r[idx]), epoch.isFinite {
                start = Date(timeIntervalSince1970: epoch)
            } else { start = end.addingTimeInterval(-secs) }
            guard end > start, end.timeIntervalSince(start) <= 3600,
                  abs(end.timeIntervalSince(start) - secs) < 0.1 else { continue }
            var cursor = start
            while cursor < end {
                guard let boundary = calendar.date(byAdding: .day, value: 1, to: calendar.startOfDay(for: cursor)) else { break }
                let stop = min(end, boundary), date = dateString(cursor, "yyyy-MM-dd")
                let interval = FocusInterval(start:cursor,end:stop)
                grouped[date,default:[:]][platform + " · " + r[indexes[2]],default:[]].append(interval)
                byPlatform[date,default:[:]][platform,default:[]].append(interval)
                result[date, default: Totals()].intervals.append(interval)
                cursor = stop
            }
        }
    }
    for day in Array(result.keys) {
        for (key, intervals) in grouped[day] ?? [:] {
            let seconds = unionSeconds(intervals)
            result[day]!.apps[key] = seconds
        }
        result[day]!.windows = unionSeconds(byPlatform[day]?["Windows"] ?? [])
        result[day]!.mac = unionSeconds(byPlatform[day]?["macOS"] ?? [])
        result[day]!.focus = unionSeconds(result[day]!.intervals)
    }
    return result
}

final class Recorder: NSObject, NSApplicationDelegate {
    let dashboard = DashboardModel()
    let root: URL
    var rules = Rules(), status: NSStatusItem!, window: NSWindow!
    var paused = false, suspended = false, locked = false, last = ProcessInfo.processInfo.systemUptime
    var pendingEnd = Date()
    var pending: Double = 0, lastApp = "", lastState = "Inactive", lastReason = "Starting", lastIdle: Double = 0
    var timer: Timer?, device = "", error = "", lockFD: Int32 = -1
    var sync: LocalSync?
    var schedule = SyncSchedule()
    var syncError = ""
    var lastStatusAttempt = Date.distantPast
    var lastWrittenState: String?
    var attemptedState: String?
    var lastRefresh = Date.distantPast, day = dateString(Date(), "yyyy-MM-dd")
	var settlementURL: URL { FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/FocusStarMac/settled-daily.csv") }
    init(root: URL) { self.root = root; super.init() }
    func applicationDidFinishLaunching(_ n: Notification) {
        let local = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/FocusStarMac")
        try? FileManager.default.createDirectory(at: local, withIntermediateDirectories: true)
        lockFD = open(local.appendingPathComponent("recorder.lock").path, O_CREAT | O_RDWR, 0o600)
        guard lockFD >= 0 && flock(lockFD, LOCK_EX | LOCK_NB) == 0 else { NSApp.terminate(nil); return }
        let idURL = local.appendingPathComponent("device-id")
        device = (try? String(contentsOf: idURL, encoding: .utf8)) ?? UUID().uuidString
        try? device.write(to: idURL, atomically: true, encoding: .utf8)
        sync = LocalSync(root:root,device:device)
        reloadRules()
        status = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        status.button?.title = "★ Focus"
        let menu = NSMenu()
        for (title, action) in [("Show Focus Star", #selector(show)),("Pause / Resume",#selector(toggle)),("Open cross-platform report",#selector(report)),("Open data folder",#selector(folder)),("Edit Mac rules",#selector(editRules)),("Quit",#selector(quit))] {
            let item = NSMenuItem(title:title,action:action,keyEquivalent:""); item.target = self; menu.addItem(item)
        }
        status.menu = menu
        dashboard.pause = { [weak self] in self?.toggle() }
        dashboard.records = { [weak self] in self?.folder() }
        dashboard.report = { [weak self] in self?.report() }
        dashboard.save = { [weak self] input,grace,goal,interval,names in
            guard let self = self else { return false }
            var updated = self.rules
            updated.inputActiveSeconds = Double(input*60); updated.workAppIdleGraceSeconds = Double(grace*60)
            updated.dailyGoalMinutes = Double(goal); updated.pollIntervalSeconds = Double(interval)
            updated.workProcessNames = names.split(separator:",").map { $0.trimmingCharacters(in:.whitespacesAndNewlines) }.filter { !$0.isEmpty }
            do {
                let data = try JSONEncoder().encode(updated)
                try data.write(to:self.root.appendingPathComponent("settings-mac.json"),options:.atomic)
                self.flush(); self.rules = updated; self.sample(); self.refresh(); return true
            } catch { self.dashboard.message = error.localizedDescription; return false }
        }
        window = NSWindow(contentRect:NSRect(x:0,y:0,width:1040,height:620),styleMask:[.titled,.closable,.miniaturizable,.resizable],backing:.buffered,defer:false)
        window.title = "Focus Star"; window.isReleasedWhenClosed = false
        window.minSize = NSSize(width:1000,height:600); window.center()
        window.contentView = NSHostingView(rootView:ContentView(model:dashboard))
        let nc = NSWorkspace.shared.notificationCenter
        nc.addObserver(self,selector:#selector(sleeping),name:NSWorkspace.willSleepNotification,object:nil)
        nc.addObserver(self,selector:#selector(waking),name:NSWorkspace.didWakeNotification,object:nil)
        nc.addObserver(self,selector:#selector(sleeping),name:NSWorkspace.sessionDidResignActiveNotification,object:nil)
        nc.addObserver(self,selector:#selector(waking),name:NSWorkspace.sessionDidBecomeActiveNotification,object:nil)
        DistributedNotificationCenter.default().addObserver(self,selector:#selector(screenLock),name:NSNotification.Name("com.apple.screenIsLocked"),object:nil)
        DistributedNotificationCenter.default().addObserver(self,selector:#selector(screenUnlock),name:NSNotification.Name("com.apple.screenIsUnlocked"),object:nil)
        sample(); syncNow(force:true); show()
        timer = Timer.scheduledTimer(timeInterval:1,target:self,selector:#selector(tick),userInfo:nil,repeats:true)
    }
    func reloadRules() {
        let url = root.appendingPathComponent("settings-mac.json")
        if !FileManager.default.fileExists(atPath:url.path) { try? FileManager.default.copyItem(at:root.appendingPathComponent("settings.json"),to:url) }
        if let d = try? Data(contentsOf:url), let r = try? JSONDecoder().decode(Rules.self,from:d), r.pollIntervalSeconds.isFinite, (1...300).contains(r.pollIntervalSeconds), (0...86400).contains(r.inputActiveSeconds), (0...86400).contains(r.workAppIdleGraceSeconds) { rules = r }
        else { error = "Invalid settings-mac.json; keeping previous rules." }
    }
    func sample() {
        let app = NSWorkspace.shared.frontmostApplication
        lastApp = aliases[app?.bundleIdentifier ?? ""] ?? app?.localizedName ?? "Unknown"
        lastIdle = CGEventSource.secondsSinceLastEventType(.combinedSessionState,eventType:CGEventType(rawValue:UInt32.max)!)
        let allowed = rules.workProcessNames.contains { $0.caseInsensitiveCompare(lastApp) == .orderedSame }
        lastState = allowed && lastIdle <= max(rules.inputActiveSeconds,rules.workAppIdleGraceSeconds) ? "Work" : "Inactive"
        lastReason = !allowed ? "App is not in the work list" : lastState == "Work" ? "Work app within activity window" : "Idle timeout"
    }
    @objc func tick() {
        let now = ProcessInfo.processInfo.systemUptime, elapsed = now-last; last = now
        let today = dateString(Date(),"yyyy-MM-dd")
        if today != day { flush(); day = today; syncNow(force:true) }
        if elapsed >= 5 || elapsed <= 0 { flush(); sample() }
        if !paused && !suspended && !locked && elapsed > 0 && elapsed < 5 {
            let oldApp = lastApp, oldState = lastState, oldReason = lastReason, oldIdle = lastIdle
            sample()
            let newApp = lastApp, newState = lastState, newReason = lastReason, newIdle = lastIdle
            if newApp != oldApp || newState != oldState {
                lastApp = oldApp; lastState = oldState; lastReason = oldReason; lastIdle = oldIdle; flush()
                lastApp = newApp; lastState = newState; lastReason = newReason; lastIdle = newIdle
            }
            pending += elapsed
            pendingEnd = Date()
            if pending >= min(60,max(30,rules.pollIntervalSeconds)) { flush() }
        }
        updateActivity()
        syncNow()
        if Date().timeIntervalSince(lastRefresh) >= 30 { reloadRules(); refresh() }
    }
    func flush() {
        guard pending > 0 else { return }
        do {
            guard let sync = sync else { throw SyncFailure.verification("Local cache unavailable") }
            // Epoch bounds preserve timezone and subsecond interval boundaries.
            let stamp = dateString(pendingEnd,"yyyy-MM-dd HH:mm:ss")
            let row = [stamp,lastState,lastReason,String(format:"%.1f",lastIdle),lastApp,String(format:"%.3f",pending),"macOS",device,String(format:"%.3f",pendingEnd.timeIntervalSince1970 - pending),String(format:"%.3f",pendingEnd.timeIntervalSince1970)]
            try sync.save(row:row,day:dateString(pendingEnd,"yyyy-MM-dd"))
            pending = 0; error = ""
        } catch { self.error = "Save failed: \(error.localizedDescription)"; paused = true }
    }
    func syncNow(force: Bool = false) {
        guard schedule.begin(force:force), let sync = sync else { return }
        flush()
        do {
            try sync.publish()
        } catch {
            syncError = "WPS write deferred; local records retained: \(error.localizedDescription)"
            refresh()
            return
        }
        do {
            try archiveHistory(root,device:device)
            syncError = ""
        } catch {
            syncError = "History archive deferred; original records retained: \(error.localizedDescription)"
        }
        refresh(writeReports:true)
    }
    func refresh(writeReports: Bool = false) {
        lastRefresh = Date()
        let localFiles: [URL]
        do { localFiles = try sync?.files() ?? [] }
        catch { localFiles = []; syncError = "Cannot read local cache: \(error.localizedDescription)" }
        var saved = loadSavedFocus(root,localLedger:settlementURL)
        let totals = applySettledFocus(summarize(root,rules,localFiles:localFiles),saved:&saved)
        do { try saveSettledFocus(saved,to:settlementURL) }
        catch { syncError = "Local settlement save deferred: \(error.localizedDescription)" }
        let today = totals[dateString(Date(),"yyyy-MM-dd")] ?? Totals()
        var all = Totals(); for t in totals.values { all.windows += t.windows; all.mac += t.mac; all.focus += t.focus }
        status.button?.title = paused ? "★ Paused" : "★ " + duration(today.focus)

        dashboard.totals = totals
        dashboard.todayFocusMinutes = Int(today.focus/60); dashboard.macMinutes = Int(today.mac/60); dashboard.windowsMinutes = Int(today.windows/60)
        dashboard.allFocusMinutes = Int(all.focus/60)
        dashboard.dailyGoalMinutes = Int(rules.dailyGoalMinutes); dashboard.inputActivityMinutes = Int(rules.inputActiveSeconds/60)
        dashboard.workAppGraceMinutes = Int(rules.workAppIdleGraceSeconds/60); dashboard.saveIntervalSeconds = Int(min(60,max(30,rules.pollIntervalSeconds)))
        dashboard.names = rules.workProcessNames.joined(separator:", "); dashboard.message = [error,syncError].filter { !$0.isEmpty }.joined(separator:" · ")
        updateActivity()

        guard writeReports else { return }
        var html = "<!doctype html><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>Focus Star · 跨设备统计</title><style>body{font:16px system-ui;max-width:1000px;margin:40px auto;padding:20px;background:#f4f6fb;color:#17213a}table{border-collapse:collapse;width:100%;background:white}th,td{padding:12px;text-align:left;border-bottom:1px solid #ddd}h1{color:#4d47c7}</style><h1>Focus Star · 跨设备专注统计</h1><p>日期：\(esc(dateString(Date(),"yyyy-MM-dd"))) · Mac 每 10 分钟批量写入，有变化才更新；Windows 收到 WPS 同步后重新打开此页面。</p><h2>今日专注时长（结算）：\(duration(today.focus))</h2><p>Mac：\(duration(today.mac))　Windows：\(duration(today.windows))</p><h2>累计专注时长（结算）：\(duration(all.focus))</h2><p>Mac：\(duration(all.mac))　Windows：\(duration(all.windows))</p><p>每日总时长取当前跨设备专注区间并集与各设备已保存合法总时长中的最大值；已保存值不会相加，较小的同步不完整结果不会拉低结算值。设备和软件明细仍按当前可读区间分别统计。旧数据用采样结束时间减采样秒数恢复时间段，并按香港时区（UTC+8）解释，日期与跨天统计固定使用香港时间。这是活动规则估算，并非注意力测量。旧版记录归入 Windows。统计按 Mac 当前工作软件白名单重新计算。</p><table><tr><th>日期</th><th>Mac</th><th>Windows</th><th>结算总时长</th></tr>"
        for d in totals.keys.sorted(by:>) { let t = totals[d]!; html += "<tr><td>\(esc(d))</td><td>\(duration(t.mac))</td><td>\(duration(t.windows))</td><td>\(duration(t.focus))</td></tr>" }
        html += "</table><h2>今天各软件</h2><table>"
        for (app,seconds) in today.apps.sorted(by:{$0.value > $1.value}) { html += "<tr><td>\(esc(app))</td><td>\(duration(seconds))</td></tr>" }
        html += "</table>"
        do {
            try writeChanged(Data(html.utf8),to:root.appendingPathComponent("跨设备专注统计.html"))
            let lines = totals.keys.sorted().map { date -> String in
                let t = totals[date]!
                return [date,String(format:"%.3f",t.windows),String(format:"%.3f",t.mac),String(format:"%.3f",t.focus)].map(cell).joined(separator:",")
            }
            let summary = "date,windows_seconds,mac_seconds,focus_seconds\n" + lines.joined(separator:"\n") + (lines.isEmpty ? "" : "\n")
            try writeChanged(Data(summary.utf8),to:reportDirectory.appendingPathComponent("daily-summary.csv"))
        } catch { syncError = "Report save deferred: \(error.localizedDescription)"; dashboard.message = syncError }
    }
    var reportDirectory: URL { root.appendingPathComponent("mac-reports").appendingPathComponent(device) }
    func writeStateIfChanged() {
        guard !device.isEmpty else { return }
        let state = dashboard.isFocus ? "Work" : "Inactive"
        guard state != lastWrittenState else { return }
        guard state != attemptedState || Date().timeIntervalSince(lastStatusAttempt) >= 30 else { return }
        attemptedState = state
        lastStatusAttempt = Date()
        do {
            try writeChanged(Data(("Current state: " + state + "\n").utf8),to:reportDirectory.appendingPathComponent("today-status.txt"))
            lastWrittenState = state
        } catch { syncError = "Status save deferred: \(error.localizedDescription)" }

    }
    func updateActivity() {
        dashboard.isPaused = paused || suspended || locked
        dashboard.currentApp = lastApp; dashboard.currentReason = lastReason
        dashboard.isFocus = lastState == "Work" && !dashboard.isPaused
        writeStateIfChanged()
        dashboard.idleSeconds = lastIdle.isFinite ? Int(min(lastIdle,Double(Int.max/2))) : 0
    }
    @objc func show() { window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps:true) }
    @objc func toggle() { flush(); paused.toggle(); last = ProcessInfo.processInfo.systemUptime; sample(); refresh() }
    @objc func sleeping() { flush(); suspended = true; syncNow(force:true) }
    @objc func waking() { suspended = false; last = ProcessInfo.processInfo.systemUptime; sample() }
    @objc func screenLock() { flush(); locked = true; updateActivity() }
    @objc func screenUnlock() { locked = false; last = ProcessInfo.processInfo.systemUptime; sample() }
    @objc func folder() { NSWorkspace.shared.open(root) }
    @objc func report() { flush(); refresh(); NSWorkspace.shared.open(root.appendingPathComponent("跨设备专注统计.html")) }
    @objc func editRules() { NSWorkspace.shared.open(root.appendingPathComponent("settings-mac.json")) }
    @objc func quit() { NSApp.terminate(nil) }
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply { if sync != nil { flush(); suspended = true; syncNow(force:true) }; return .terminateNow }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender:NSApplication) -> Bool { false }
}

if CommandLine.arguments.contains("--self-test") {
    let zoneTest = ISO8601DateFormatter().date(from:"2026-09-22T16:30:00Z")!
    assert(dateString(zoneTest,"yyyy-MM-dd HH:mm") == "2026-09-23 00:30")
    assert(zoneTest.timeIntervalSince(focusCalendar.startOfDay(for:zoneTest)) == 1800)
    print("PASS: UTC+08:00 date and midnight independent of system timezone")
    assert(csv("\u{feff}a,b\r\n\"x,y\",\"q\"\"z\"\r\npartial").last! == ["x,y","q\"z"])
	var saved: [String:Double] = [:]
	mergeSavedFocusCSV("date,focus_seconds,windows_seconds,mac_seconds,timezone,sync_status\n2026-01-01,3600,0,0,UTC+08:00,ok\n2026-01-01,5400,0,0,UTC+08:00,ok\n2026-02-30,7000,0,0,UTC+08:00,bad\n2026-01-02,-1,0,0,UTC+08:00,bad\n2026-01-03,90000,0,0,UTC+08:00,bad\n2026-01-04,nan,0,0,UTC+08:00,bad\n2026-01-05,7200,0,0,UTC+00:00,bad\n2026-01-06,1800,0,0,UTC+08:00",into:&saved)
	mergeSavedFocusCSV("date,windows_seconds,mac_seconds,focus_seconds\n2026-01-01,0,0,4500\n2026-01-07,0,0,1200\n",into:&saved)
	assert(saved.count == 2 && saved["2026-01-01"] == 5400 && saved["2026-01-07"] == 1200)
	var lower = Totals(); lower.windows = 3600; lower.focus = 3600
	var higher = Totals(); higher.windows = 2400; higher.focus = 2400
	var computed: [String:Totals] = ["2026-01-01":lower, "2026-01-07":higher]
	computed = applySettledFocus(computed,saved:&saved)
	assert(computed["2026-01-01"]!.focus == 5400 && computed["2026-01-07"]!.focus == 2400)
    var rules = Rules(); rules.workProcessNames = ["Code"]
    assert(isWork("Work","code",rules)); assert(!isWork("Work","Safari",rules)); assert(!isWork("Inactive","Code",rules))
    let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
    try! FileManager.default.createDirectory(at:root.appendingPathComponent("data"),withIntermediateDirectories:true)
    try! FileManager.default.createDirectory(at:root.appendingPathComponent("mac-data/test"),withIntermediateDirectories:true)
    let header = "timestamp,state,reason,idle_seconds,foreground_process,sample_seconds\n"
    try! (header + "2026-01-01 12:00:00,Work,ok,0,Code,60\n2026-01-01 12:01:00,Work,ok,0,Game,120\n").write(to:root.appendingPathComponent("data/2026-01-01.csv"),atomically:true,encoding:.utf8)
    try! (header + "2026-01-01 12:00:00,Work,ok,0,Code,30\n2026-01-01 12:00:00,Work,ok,0,Code,nan\n").write(to:root.appendingPathComponent("mac-data/test/2026-01-01.csv"),atomically:true,encoding:.utf8)
    let total = summarize(root,rules)["2026-01-01"]!; assert(total.windows == 60 && total.mac == 30 && total.focus == 60)
    func interval(_ a: Double, _ b: Double) -> FocusInterval { FocusInterval(start: Date(timeIntervalSince1970:a),end: Date(timeIntervalSince1970:b)) }
    assert(unionSeconds([interval(0,3600),interval(1800,5400)]) == 5400)
    assert(unionSeconds([interval(0,60),interval(0,60),interval(10,20)]) == 60)
    assert(unionSeconds([interval(0,60),interval(60,90),interval(120,180)]) == 150)
    assert(unionSeconds([]) == 0)
    try! (header + "2026-01-02 00:00:30,Work,ok,0,Code,60\n").write(to:root.appendingPathComponent("data/2026-01-02.csv"),atomically:true,encoding:.utf8)
    let days = summarize(root,rules)
    assert(days["2026-01-01"]!.focus == 90 && days["2026-01-02"]!.focus == 30)
    let epoch = DateFormatter(); epoch.timeZone = focusTimeZone; epoch.locale = Locale(identifier:"en_US_POSIX"); epoch.dateFormat = "yyyy-MM-dd HH:mm:ss"
    let end = epoch.date(from:"2026-01-02 00:00:30")!.timeIntervalSince1970
    let v2header = header.trimmingCharacters(in:.newlines) + ",start_epoch,end_epoch\n"
    try! (v2header + "bad-display-date,Work,ok,0,Code,60,\(end-60),\(end)\n").write(to:root.appendingPathComponent("mac-data/test/2026-01-02.csv"),atomically:true,encoding:.utf8)
    assert(summarize(root,rules)["2026-01-02"]!.focus == 30)
    // Archiving preserves totals, whitelist reclassification, midnight and late data.
    try! (header + "2026-01-01 12:00:00,Work,ok,0,Code,30\n").write(to:root.appendingPathComponent("mac-data/test/2026-01-01.csv"),atomically:true,encoding:.utf8)
    let beforeArchive = summarize(root,rules)
    let rawURL = root.appendingPathComponent("mac-data/test/2026-01-01.csv")
    let rawSnapshot = try! Data(contentsOf:rawURL)
    try! archiveHistory(root,device:"test",now:epoch.date(from:"2026-01-03 12:00:00")!)
    let afterArchive = summarize(root,rules)
    assert(afterArchive["2026-01-01"]!.focus == beforeArchive["2026-01-01"]!.focus)
    assert(afterArchive["2026-01-01"]!.mac == beforeArchive["2026-01-01"]!.mac)
    assert(afterArchive["2026-01-02"]!.focus == beforeArchive["2026-01-02"]!.focus)
    assert(!FileManager.default.fileExists(atPath:rawURL.path))
    // A raw file can reappear from WPS after cleanup; neither device nor app totals inflate.
    try! rawSnapshot.write(to:rawURL)
    assert(summarize(root,rules)["2026-01-01"]!.mac == afterArchive["2026-01-01"]!.mac)
    try! archiveHistory(root,device:"test",now:epoch.date(from:"2026-01-03 12:00:00")!)
    let todayURL = root.appendingPathComponent("mac-data/test/2026-01-03.csv")
    try! (header + "2026-01-03 12:00:00,Work,ok,0,Code,30\n").write(to:todayURL,atomically:true,encoding:.utf8)
    try! archiveHistory(root,device:"test",now:epoch.date(from:"2026-01-03 13:00:00")!)
    assert(FileManager.default.fileExists(atPath:todayURL.path))
    let brokenURL = root.appendingPathComponent("mac-data/test/2026-01-02.csv")
    try! (header + "partial").write(to:brokenURL,atomically:true,encoding:.utf8)
    do { try archiveHistory(root,device:"test",now:epoch.date(from:"2026-01-03 13:00:00")!); assertionFailure("Should retain incomplete input") } catch {}
    assert(FileManager.default.fileExists(atPath:brokenURL.path))
    try! FileManager.default.removeItem(at:brokenURL)
    let mislabeledURL = root.appendingPathComponent("mac-data/test/2026-01-02.csv")
    let localDayEpoch = epoch.date(from:"2026-01-03 10:00:00")!.timeIntervalSince1970
    let mislabeled = "timestamp,state,reason,idle_seconds,foreground_process,sample_seconds,platform,device_id,start_epoch,end_epoch\n" +
        "\"2026-01-02 18:00:00\",\"Work\",\"ok\",\"0\",\"Code\",\"60.000\",\"macOS\",\"test\",\"\(localDayEpoch-60)\",\"\(localDayEpoch)\"\n"
    try! mislabeled.write(to:mislabeledURL,atomically:true,encoding:.utf8)
    try! archiveHistory(root,device:"test",now:epoch.date(from:"2026-01-03 13:00:00")!)
    assert(FileManager.default.fileExists(atPath:mislabeledURL.path))
    assert(summarize(root,rules)["2026-01-03"]!.focus == 90)
    try! archiveHistory(root,device:"test",now:epoch.date(from:"2026-01-04 13:00:00")!)
    assert(!FileManager.default.fileExists(atPath:mislabeledURL.path))
    assert(summarize(root,rules)["2026-01-03"]!.focus == 90)
    // Local durability, replay after process restart and idempotent cloud merge.
    let queueRoot = root.appendingPathComponent("batch-test")
    let queueBase = root.appendingPathComponent("local-only")
    let queue = LocalSync(root:queueRoot,device:"mac-test",base:queueBase)
    let sample = ["2026-01-02 00:00:30","Work","test","0","Code","30.000","macOS","mac-test",String(end-30),String(end)]
    try! queue.save(row:sample,day:"2026-01-02")
    let cloudFile = queueRoot.appendingPathComponent("mac-data/mac-test/v2/2026-01-02.csv")
    assert(!FileManager.default.fileExists(atPath:cloudFile.path))
    assert(summarize(queueRoot,rules,localFiles:try! queue.files())["2026-01-02"]!.focus == 30)
    let restarted = LocalSync(root:queueRoot,device:"mac-test",base:queueBase)
    try! restarted.publish()
    assert(try! restarted.files().isEmpty)
    assert(try! sampleRows(Data(contentsOf:cloudFile),path:cloudFile.path).count == 1)
    // Simulate crash after shared CSV committed but before local queue removal.
    try! restarted.save(row:sample,day:"2026-01-02")
    assert(summarize(queueRoot,rules,localFiles:try! restarted.files())["2026-01-02"]!.mac == 30)
    try! restarted.publish()
    assert(try! sampleRows(Data(contentsOf:cloudFile),path:cloudFile.path).count == 1)
    // Incomplete destination must not be overwritten; outbox survives a failure.
    try! restarted.save(row:sample,day:"2026-01-02")
    try! Data("partial".utf8).write(to:cloudFile)
    do { try restarted.publish(); assertionFailure("Partial cloud destination accepted") } catch {}
    assert(!(try! restarted.files().isEmpty))
    assert(try! String(contentsOf:cloudFile,encoding:.utf8) == "partial")
    try! encodeSamples([]).write(to:cloudFile)
    try! restarted.publish()
    assert(summarize(queueRoot,rules)["2026-01-02"]!.focus == 30)
    // No-change writes keep the original file modification time.
    let oldDate = Date(timeIntervalSince1970:1000)
    try! FileManager.default.setAttributes([.modificationDate:oldDate],ofItemAtPath:cloudFile.path)
    try! writeChanged(Data(contentsOf:cloudFile),to:cloudFile)
    assert((try! FileManager.default.attributesOfItem(atPath:cloudFile.path)[.modificationDate] as! Date) == oldDate)
    var cadence = SyncSchedule()
    let baseTime = Date(timeIntervalSince1970:10000)
    assert(cadence.begin(now:baseTime))
    assert(!cadence.begin(now:baseTime.addingTimeInterval(599)))
    assert(cadence.begin(now:baseTime.addingTimeInterval(600)))
    assert(cadence.begin(now:baseTime.addingTimeInterval(601),force:true))
    assert(!cadence.begin(now:baseTime.addingTimeInterval(602)))
    // Both Windows devices own separate daily files. Their overlapping focus is a union,
    // including when a legacy shared file reappears and when the Mac overlaps both.
    let multiRoot = root.appendingPathComponent("multi-windows")
    let firstWin = multiRoot.appendingPathComponent("windows-data/win-a/v2")
    let secondWin = multiRoot.appendingPathComponent("windows-data/win-b/v2")
    let macWin = multiRoot.appendingPathComponent("mac-data/mac/v2")
    try! FileManager.default.createDirectory(at:firstWin,withIntermediateDirectories:true)
    try! FileManager.default.createDirectory(at:secondWin,withIntermediateDirectories:true)
    try! FileManager.default.createDirectory(at:macWin,withIntermediateDirectories:true)
    try! FileManager.default.createDirectory(at:multiRoot.appendingPathComponent("data"),withIntermediateDirectories:true)
    let morning = epoch.date(from:"2026-01-04 10:00:00")!.timeIntervalSince1970
    let winHeader = "timestamp,state,reason,idle_seconds,foreground_process,sample_seconds,platform,device_id,start_epoch,end_epoch\n"
    func winRow(_ start: Double, _ end: Double, _ platform: String, _ device: String) -> String {
        ["2026-01-04 11:30:00","Work","test","0","Code",String(end-start),platform,device,String(start),String(end)].map(cell).joined(separator:",") + "\n"
    }
    try! (winHeader + winRow(morning,morning+3600,"Windows","win-a")).write(to:firstWin.appendingPathComponent("2026-01-04.csv"),atomically:true,encoding:.utf8)
    try! (winHeader + winRow(morning+1800,morning+5400,"Windows","win-b")).write(to:secondWin.appendingPathComponent("2026-01-04.csv"),atomically:true,encoding:.utf8)
    try! (winHeader + winRow(morning+2700,morning+4500,"macOS","mac")).write(to:macWin.appendingPathComponent("2026-01-04.csv"),atomically:true,encoding:.utf8)
    try! (winHeader + winRow(morning+1800,morning+5400,"Windows","win-b")).write(to:multiRoot.appendingPathComponent("data/2026-01-04.csv"),atomically:true,encoding:.utf8)
    let multiTotal = summarize(multiRoot,rules)["2026-01-04"]!
    assert(multiTotal.windows == 5400 && multiTotal.mac == 1800 && multiTotal.focus == 5400)
    assert(multiTotal.apps["Windows · Code"] == 5400)
    print("PASS: local-only saves, restart recovery, replay deduplication, failed publish retention, unchanged writes, 600-second/forced schedule")
    try! FileManager.default.removeItem(at:root)
    print("PASS: CSV quoting/BOM/partial rows, whitelist, platform aggregation, invalid numbers, overlap/duplicate/disjoint intervals, midnight splitting, epoch bounds")
} else {
    let root = Bundle.main.bundleURL.deletingLastPathComponent()
    let app = NSApplication.shared; app.setActivationPolicy(.regular)
    let menuBar = NSMenu(), appMenuItem = NSMenuItem(), appMenu = NSMenu()
    appMenu.addItem(withTitle:"Quit Focus Star",action:#selector(NSApplication.terminate(_:)),keyEquivalent:"q")
    appMenuItem.submenu = appMenu; menuBar.addItem(appMenuItem); app.mainMenu = menuBar
    let delegate = Recorder(root:root); app.delegate = delegate; app.run()
}
