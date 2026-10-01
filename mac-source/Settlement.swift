import Foundation

// Reports are lower bounds from another device's more complete snapshot, not
// independent durations to add. Raw intervals remain the source of app detail.
func settleTotals(_ input: [String:Totals], root: URL, checkpoints: [URL] = [], now: Date = Date()) -> [String:Totals] {
    var result = input
    var files = checkpoints
    for directory in ["mac-reports","windows-reports"] {
        if let e = FileManager.default.enumerator(at:root.appendingPathComponent(directory),includingPropertiesForKeys:nil) {
            for case let u as URL in e where u.pathExtension == "csv" && u.lastPathComponent.hasPrefix("daily-summary") { files.append(u) }
        }
    }
    let f = DateFormatter(); f.timeZone = focusTimeZone; f.locale = Locale(identifier:"en_US_POSIX"); f.dateFormat = "yyyy-MM-dd"; f.isLenient = false
    for url in files {
        guard let text = try? String(contentsOf:url,encoding:.utf8) else { continue }
        let rows = csv(text)
        guard let h = rows.first, let di = h.firstIndex(of:"date"), let fi = h.firstIndex(of:"focus_seconds"), let wi = h.firstIndex(of:"windows_seconds"), let mi = h.firstIndex(of:"mac_seconds") else { continue }
        for row in rows.dropFirst() {
            guard row.count == h.count, let day = f.date(from:row[di]), f.string(from:day) == row[di], day <= now,
                  let end = focusCalendar.date(byAdding:.day,value:1,to:day),
                  let focus = Double(row[fi]), let windows = Double(row[wi]), let mac = Double(row[mi]),
                  [focus,windows,mac].allSatisfy({$0.isFinite && $0 >= 0 && $0 <= end.timeIntervalSince(day)+0.1}),
                  focus + 1 >= max(windows,mac), focus <= windows+mac+1 else { continue }
            var t = result[row[di]] ?? Totals()
            t.windows = max(t.windows,windows); t.mac = max(t.mac,mac)
            t.focus = max(t.focus,max(focus,max(t.windows,t.mac)))
            result[row[di]] = t
        }
    }
    return result
}
func settlementCSV(_ totals: [String:Totals]) -> Data {
    let lines = totals.keys.sorted().map { day -> String in
        let t = totals[day]!
        return [day,String(format:"%.3f",t.windows),String(format:"%.3f",t.mac),String(format:"%.3f",t.focus)].map(cell).joined(separator:",")
    }
    return Data(("date,windows_seconds,mac_seconds,focus_seconds\n" + lines.map { $0 + "\n" }.joined()).utf8)
}
func testSettlement() {
    let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
    defer { try? FileManager.default.removeItem(at:root) }
    let report = root.appendingPathComponent("windows-reports/test/daily-summary.csv")
    var observed = Totals(); observed.windows = 18000; observed.mac = 21000; observed.focus = 28080
    var stale = Totals(); stale.windows = 18000; stale.mac = 5000; stale.focus = 23000
    try! writeChanged(settlementCSV(["2026-01-01":stale]),to:report)
    let full = settleTotals(["2026-01-01":observed],root:root)
    assert(full["2026-01-01"]!.focus == 28080)
    let checkpoint = root.appendingPathComponent("settlement.snapshot")
    try! writeChanged(settlementCSV(full),to:checkpoint)
    // Lost/late raw files and smaller later reports must not reduce settlement.
    assert(settleTotals([:],root:root,checkpoints:[checkpoint])["2026-01-01"]!.focus == 28080)
    observed.focus = 30000
    try! writeChanged(settlementCSV(["2026-01-01":observed]),to:report)
    assert(settleTotals(full,root:root)["2026-01-01"]!.focus == 30000)
    // Ignore NaN, impossible totals, negatives and future dates.
    let invalid = "date,windows_seconds,mac_seconds,focus_seconds\n2026-01-02,0,0,nan\n2026-01-03,1,1,50000\n2026-01-04,-1,1,1\n9999-01-01,1,1,1\n"
    try! writeChanged(Data(invalid.utf8),to:report)
    assert(settleTotals([:],root:root).isEmpty)
    print("PASS: settlement maximum, lower stale report, persistent checkpoint, later fuller report, invalid summary rejection")
}
