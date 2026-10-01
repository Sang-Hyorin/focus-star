import Foundation
import SwiftUI

enum FocusTheme {
    static let background = Color(red: 0.965, green: 0.973, blue: 0.988)
    static let surface = Color.white
    static let accent = Color(red: 0.325, green: 0.357, blue: 0.949)
    static let accentTwo = Color(red: 0.522, green: 0.361, blue: 0.965)
    static let text = Color(red: 0.106, green: 0.122, blue: 0.165)
    static let muted = Color(red: 0.439, green: 0.467, blue: 0.537)
    static let line = Color(red: 0.902, green: 0.914, blue: 0.945)
    static let green = Color(red: 0.137, green: 0.643, blue: 0.439)
    static let greenSoft = Color(red: 0.902, green: 0.973, blue: 0.941)
    static let amber = Color(red: 0.804, green: 0.518, blue: 0.133)
    static let amberSoft = Color(red: 1.0, green: 0.965, blue: 0.886)
}

enum HistoryRange: String, CaseIterable, Identifiable {
    case daily = "Daily"
    case weekly = "Weekly"
    case monthly = "Monthly"
    case yearly = "Yearly"

    var id: String { rawValue }
}

struct HistoryPoint: Identifiable {
    let id = UUID()
    let label: String
    let detail: String
    let hours: Double
}

struct AppUsage: Identifiable {
    let id = UUID()
    let name: String
    let symbol: String
    let hours: Double
    let sessions: Int
    let tint: Color
}


final class DashboardModel: ObservableObject {
    @Published var isPaused = false
    @Published var currentApp = "Starting"
    @Published var currentReason = ""
    @Published var isFocus = false
    @Published var idleSeconds = 0
    @Published var todayFocusMinutes = 0
    @Published var macMinutes = 0
    @Published var windowsMinutes = 0
    @Published var allFocusMinutes = 0
    @Published var dailyGoalMinutes = 480
    @Published var inputActivityMinutes = 1
    @Published var workAppGraceMinutes = 3
    @Published var saveIntervalSeconds = 30
    @Published var names = ""
    @Published var message = ""
    @Published var totals: [String: Totals] = [:]
    var pause: () -> Void = {}
    var records: () -> Void = {}
    var report: () -> Void = {}
    var save: (Int, Int, Int, Int, String) -> Bool = { _,_,_,_,_ in false }
    var focusProgress: Double { dailyGoalMinutes > 0 ? min(Double(todayFocusMinutes)/Double(dailyGoalMinutes),1) : 0 }
    func usage(_ range: String) -> [AppUsage] {
        let count = range == "Today" ? 1 : range == "7 Days" ? 7 : range == "30 Days" ? 30 : 100000
        let cutoff = count == 100000 ? "" : dateString(focusCalendar.date(byAdding:.day,value:1-count,to:Date())!,"yyyy-MM-dd")
        var values: [String:Double] = [:]
        for (day, t) in totals where day >= cutoff { for (name, seconds) in t.apps { values[name,default:0] += seconds } }
        return values.sorted { $0.value > $1.value }.map { AppUsage(name:$0.key,symbol:"app.fill",hours:$0.value/3600,sessions:0,tint:FocusTheme.accent) }
    }
    func history(for range: HistoryRange) -> [HistoryPoint] {
        let cal = focusCalendar
        let component: Calendar.Component = range == .daily ? .day : range == .weekly ? .weekOfYear : range == .monthly ? .month : .year
        let count = range == .daily ? 14 : range == .yearly ? 6 : 12
        let now = cal.dateInterval(of:component,for:Date())!.start
        return (0..<count).reversed().map { offset in
            let start = cal.date(byAdding:component,value: -offset,to:now)!
            let end = cal.date(byAdding:component,value:1,to:start)!
            let lower = dateString(start,"yyyy-MM-dd"), upper = dateString(end,"yyyy-MM-dd")
            let seconds = totals.filter { $0.key >= lower && $0.key < upper }.values.reduce(0) { $0 + $1.focus }
            let detail = range == .weekly ? lower + " – " + dateString(cal.date(byAdding:.day,value:-1,to:end)!,"yyyy-MM-dd") : dateString(start,range == .yearly ? "yyyy" : range == .monthly ? "yyyy-MM" : "yyyy-MM-dd")
            return HistoryPoint(label:dateString(start,range == .yearly ? "yyyy" : range == .monthly ? "yyyy-MM" : "MM-dd"),detail:detail,hours:seconds/3600)
        }
    }
}
func durationText(minutes: Int) -> String { "\(minutes/60)h \(minutes%60)m" }
func hourText(_ hours: Double) -> String { durationText(minutes:Int(hours*60)) }
