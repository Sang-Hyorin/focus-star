import SwiftUI
import Charts
import AppKit

private enum DashboardSheet: String, Identifiable {
    case apps
    case history
    case transfer
    case rules

    var id: String { rawValue }
}

struct ContentView: View {
    @ObservedObject var model: DashboardModel
    @State private var presentedSheet: DashboardSheet?

    var body: some View {
        ZStack {
            FocusTheme.background.ignoresSafeArea()

            VStack(spacing: 22) {
                header

                HStack(alignment: .top, spacing: 18) {
                    focusCard
                    statusColumn
                }

                footer
            }
            .padding(.horizontal, 28)
            .padding(.top, 20)
            .padding(.bottom, 18)
        }
        .foregroundStyle(FocusTheme.text)
        .preferredColorScheme(.light)
        .overlay(alignment:.bottom) { if !model.message.isEmpty { Text(model.message).font(.caption).foregroundColor(.red).padding(6) } }
        .sheet(item: $presentedSheet) { sheet in
            switch sheet {
            case .apps:
                AppsView(model:model)
            case .history:
                HistoryView(model:model)
            case .transfer:
                TransferView(model:model)
            case .rules:
                RulesView(model:model)
            }
        }
    }

    private var header: some View {
        HStack(spacing: 13) {
            StarMark(size: 46)

            VStack(alignment: .leading, spacing: 3) {
                Text("Focus Star")
                    .font(.system(size: 25, weight: .bold, design: .rounded))
                Text("Private activity tracking, stored on your device")
                    .font(.system(size: 12.5, weight: .medium))
                    .foregroundStyle(FocusTheme.muted)
            }

            Spacer()

            StatusPill(
                title: model.isPaused ? "PAUSED" : "TRACKING",
                color: model.isPaused ? FocusTheme.amber : FocusTheme.green,
                background: model.isPaused ? FocusTheme.amberSoft : FocusTheme.greenSoft
            )
        }
    }

    private var focusCard: some View {
        VStack(alignment: .leading, spacing: 18) {
            Label("TODAY'S FOCUS · DEDUPLICATED", systemImage: "sparkles")
                .sectionLabel()

            HStack(spacing: 28) {
                ZStack {
                    Circle()
                        .stroke(FocusTheme.line, style: StrokeStyle(lineWidth: 13, lineCap: .round))
                    Circle()
                        .trim(from: 0, to: model.focusProgress)
                        .stroke(
                            AngularGradient(
                                colors: [FocusTheme.accent, FocusTheme.accentTwo],
                                center: .center
                            ),
                            style: StrokeStyle(lineWidth: 13, lineCap: .round)
                        )
                        .rotationEffect(.degrees(-90))
                        .animation(.spring(response: 0.5), value: model.focusProgress)

                    VStack(spacing: 2) {
                        Text(durationText(minutes: model.todayFocusMinutes))
                            .font(.system(size: 25, weight: .bold, design: .rounded))
                        Text("of \(durationText(minutes: model.dailyGoalMinutes))")
                            .font(.system(size: 11.5, weight: .semibold))
                            .foregroundStyle(FocusTheme.muted)
                    }
                }
                .frame(width: 158, height: 158)

                VStack(alignment: .leading, spacing: 16) {
                    MetricRow(
                        title: "Focus",
                        value: durationText(minutes: model.todayFocusMinutes),
                        color: FocusTheme.accent
                    )
                    MetricRow(
                        title: "All-time focus",
                        value: durationText(minutes: model.allFocusMinutes),
                        color: FocusTheme.amber
                    )

                    Text("Mac \(durationText(minutes:model.macMinutes)) · Windows \(durationText(minutes:model.windowsMinutes))")
                        .font(.system(size: 11.5, weight: .medium))
                        .foregroundStyle(FocusTheme.muted)
                }
            }
            .frame(maxWidth: .infinity)

            Divider().overlay(FocusTheme.line)

            HStack(spacing: 10) {
                DashboardButton(
                    title: model.isPaused ? "Resume" : "Pause",
                    symbol: model.isPaused ? "play.fill" : "pause.fill",
                    isPrimary: true
                ) {
                    model.pause()
                }
                DashboardButton(title: "Apps", symbol: "square.grid.2x2.fill") {
                    presentedSheet = .apps
                }
                DashboardButton(title: "History", symbol: "chart.bar.fill") {
                    presentedSheet = .history
                }
                DashboardButton(title: "Records", symbol: "folder.fill") {
                    model.records()
                }
            }
        }
        .padding(22)
        .frame(maxWidth: .infinity, minHeight: 402, alignment: .topLeading)
        .focusCard()
    }

    private var statusColumn: some View {
        VStack(spacing: 18) {
            VStack(alignment: .leading, spacing: 16) {
                Label("CURRENT ACTIVITY", systemImage: "waveform.path.ecg")
                    .sectionLabel()

                HStack(spacing: 12) {
                    RoundedRectangle(cornerRadius: 12, style: .continuous)
                        .fill(FocusTheme.accent.opacity(0.11))
                        .frame(width: 48, height: 48)
                        .overlay {
                            Image(systemName: "terminal.fill")
                                .font(.system(size: 20, weight: .semibold))
                                .foregroundStyle(FocusTheme.accent)
                        }

                    VStack(alignment: .leading, spacing: 4) {
                        Text(model.currentApp)
                            .font(.system(size: 17, weight: .bold, design: .rounded))
                        Text(model.isPaused ? "Not recording" : model.currentReason)
                            .font(.system(size: 11.5, weight: .medium))
                            .foregroundStyle(FocusTheme.muted)
                    }

                    Spacer()
                }

                HStack {
                    StatusDot(color: model.isPaused || !model.isFocus ? FocusTheme.amber : FocusTheme.green)
                    Text(model.isPaused ? "Paused" : model.isFocus ? "Counting as focus" : "Not counting as focus")
                        .font(.system(size: 12, weight: .semibold))
                    Spacer()
                    Text("Idle \(model.idleSeconds)s")
                        .font(.system(size: 11.5, weight: .medium))
                        .foregroundStyle(FocusTheme.muted)
                }
            }
            .padding(20)
            .frame(maxWidth: .infinity, alignment: .leading)
            .focusCard()

            VStack(alignment: .leading, spacing: 14) {
                Label("TRACKING RULES", systemImage: "slider.horizontal.3")
                    .sectionLabel()

                RuleLine(label: "Input activity", value: "\(model.inputActivityMinutes) min")
                RuleLine(label: "Work app grace", value: "\(model.workAppGraceMinutes) min")
                RuleLine(label: "Local save interval", value: "\(model.saveIntervalSeconds) sec")

                Divider().overlay(FocusTheme.line)

                HStack(spacing: 10) {
                    DashboardButton(title: "Edit rules", symbol: "pencil") {
                        presentedSheet = .rules
                    }
                    DashboardButton(title: "Transfer", symbol: "arrow.up.arrow.down") {
                        presentedSheet = .transfer
                    }
                }
            }
            .padding(20)
            .frame(maxWidth: .infinity, alignment: .leading)
            .focusCard()
        }
        .frame(width: 380)
    }

    private var footer: some View {
        HStack {
            Label("No keystrokes, window titles, document names, or web addresses are recorded.", systemImage: "lock.fill")
                .font(.system(size: 11.5, weight: .medium))
                .foregroundStyle(FocusTheme.muted)

            Spacer()

            Button("Open data folder") {
                model.records()
            }
            .buttonStyle(.plain)
            .font(.system(size: 11.5, weight: .bold))
            .foregroundStyle(FocusTheme.accent)
        }
    }
}

struct AppsView: View {
    @ObservedObject var model: DashboardModel
    private var apps: [AppUsage] { model.usage(range) }
    @Environment(\.dismiss) private var dismiss
    @State private var range = "Today"
    private let ranges = ["Today", "7 Days", "30 Days", "All Time"]

    var body: some View {
        SheetScaffold(
            title: "Apps",
            subtitle: "Focus time from approved work apps only",
            symbol: "square.grid.2x2.fill",
            dismiss: dismiss
        ) {
            VStack(spacing: 18) {
                Picker("Range", selection: $range) {
                    ForEach(ranges, id: \.self) { Text($0) }
                }
                .pickerStyle(.segmented)

                HStack(spacing: 14) {
                    SummaryTile(
                        label: "MOST USED",
                        value: apps.first?.name ?? "—",
                        detail: hourText(apps.first?.hours ?? 0),
                        symbol: "crown.fill",
                        color: FocusTheme.accent
                    )
                    SummaryTile(label:"DEVICE BREAKDOWN", value:"Mac + Windows", detail:"App times may overlap; main total is deduplicated", symbol:"desktopcomputer", color:FocusTheme.amber)
                }

                VStack(spacing: 0) {
                    ForEach(Array(apps.enumerated()), id: \.element.id) { index, app in
                        AppUsageRow(app: app, maximum: apps.first?.hours ?? 1)
                        if index < apps.count - 1 {
                            Divider().overlay(FocusTheme.line)
                        }
                    }
                }
                .padding(.horizontal, 18)
                .focusCard()
            }
        }
    }
}

struct HistoryView: View {
    @ObservedObject var model: DashboardModel
    @Environment(\.dismiss) private var dismiss
    @State private var range: HistoryRange = .daily
    @State private var hoveredPeriod: String?

    private var points: [HistoryPoint] { model.history(for: range) }

    var body: some View {
        SheetScaffold(
            title: "History",
            subtitle: "Mac + Windows focus, overlapping time counted once",
            symbol: "chart.bar.fill",
            dismiss: dismiss
        ) {
            VStack(spacing: 18) {
                Picker("Range", selection: $range) {
                    ForEach(HistoryRange.allCases) { Text($0.rawValue).tag($0) }
                }
                .pickerStyle(.segmented)

                HStack(spacing: 14) {
                    SummaryTile(
                        label: "TOTAL FOCUS",
                        value: hourText(points.reduce(0) { $0 + $1.hours }),
                        detail: range.rawValue.lowercased(),
                        symbol: "star.fill",
                        color: FocusTheme.accent
                    )
                    SummaryTile(
                        label: "AVERAGE",
                        value: hourText(points.isEmpty ? 0 : points.reduce(0) { $0 + $1.hours } / Double(points.count)),
                        detail: "per period",
                        symbol: "gauge.with.dots.needle.50percent",
                        color: FocusTheme.green
                    )
                }

                Chart(points) { point in
                    BarMark(
                        x: .value("Period", point.label),
                        y: .value("Hours", point.hours)
                    )
                    .foregroundStyle(
                        LinearGradient(
                            colors: [FocusTheme.accent, FocusTheme.accentTwo],
                            startPoint: .bottom,
                            endPoint: .top
                        )
                    )
                    .cornerRadius(5)
                    .annotation(position: .top) {
                        if points.count <= 7 {
                            Text(hourText(point.hours))
                                .font(.system(size: 9, weight: .semibold))
                                .foregroundStyle(FocusTheme.muted)
                        }
                    }
                }
                .chartYAxis {
                    AxisMarks(position: .leading) {
                        AxisGridLine(stroke: StrokeStyle(lineWidth: 1, dash: [4]))
                            .foregroundStyle(FocusTheme.line)
                        AxisValueLabel()
                    }
                }
                .chartXAxis {
                    AxisMarks(values: .automatic(desiredCount: 7)) {
                        AxisValueLabel()
                    }
                }
                .chartOverlay { proxy in
                    GeometryReader { geometry in
                        let plot = geometry[proxy.plotAreaFrame]
                        ZStack(alignment: .topLeading) {
                            Rectangle().fill(Color.clear).contentShape(Rectangle())
                                .onContinuousHover { phase in
                                    switch phase {
                                    case .active(let location):
                                        if plot.contains(location) {
                                            hoveredPeriod = proxy.value(atX: location.x - plot.minX, as: String.self)
                                        } else { hoveredPeriod = nil }
                                    case .ended:
                                        hoveredPeriod = nil
                                    }
                                }
                            if let label = hoveredPeriod,
                               let point = points.first(where: { $0.label == label }),
                               let x = proxy.position(forX: label) {
                                VStack(alignment: .leading, spacing: 4) {
                                    Text(point.detail).font(.system(size: 12, weight: .medium))
                                    Text(hourText(point.hours)).font(.system(size: 16, weight: .bold)).monospacedDigit()
                                }
                                .foregroundStyle(FocusTheme.text)
                                .padding(10)
                                .frame(width: 190, alignment: .leading)
                                .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 10))
                                .overlay(RoundedRectangle(cornerRadius: 10).stroke(FocusTheme.line))
                                .shadow(color: .black.opacity(0.12), radius: 5, y: 2)
                                .position(x: min(max(plot.minX + x, 100), max(100, geometry.size.width - 100)), y: plot.minY + 36)
                                .allowsHitTesting(false)
                            }
                        }
                    }
                }
                .onChange(of: range) { _ in hoveredPeriod = nil }
                .frame(height: 270)
                .padding(20)
                .focusCard()
            }
        }
    }
}

struct TransferView: View {
    @ObservedObject var model: DashboardModel
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        SheetScaffold(
            title: "Transfer",
            subtitle: "Move settings and history between your devices",
            symbol: "arrow.up.arrow.down",
            dismiss: dismiss
        ) {
            VStack(spacing: 16) {
                TransferCard(
                    title: "Cross-device statistics",
                    detail: "Open the real report generated from Windows and Mac records.",
                    symbol: "square.and.arrow.up.fill",
                    button: "Open Report",
                    color: FocusTheme.accent, action:model.report
                )
                TransferCard(
                    title: "Shared WPS folder",
                    detail: "WPS syncs the records. Copy the entire folder for a complete backup.",
                    symbol: "square.and.arrow.down.fill",
                    button: "Open Folder",
                    color: FocusTheme.green, action:model.records
                )

                Label("Windows .focusstar archive import is not supported here. Existing shared CSV records are read automatically.", systemImage: "lock.shield.fill")
                    .font(.system(size: 11.5, weight: .medium))
                    .foregroundStyle(FocusTheme.muted)
                    .padding(.top, 4)
            }
        }
    }
}

struct RulesView: View {
    @Environment(\.dismiss) private var dismiss
    @ObservedObject var model: DashboardModel
    @State private var input = 1
    @State private var grace = 3
    @State private var goal = 480
    @State private var interval = 30
    @State private var names = ""
    var body: some View {
        SheetScaffold(title:"Tracking Rules",subtitle:"Changes apply to this Mac; totals use this work-app list",symbol:"slider.horizontal.3",dismiss:dismiss) {
            VStack(spacing:10) {
                RuleStepper(title:"Input activity",detail:"Recent keyboard or mouse activity",value:$input,range:0...1440,suffix:"min")
                RuleStepper(title:"Work app grace",detail:"Count eligible foreground apps within this idle time",value:$grace,range:0...1440,suffix:"min")
                RuleStepper(title:"Daily goal",detail:"Deduplicated cross-device focus",value:$goal,range:1...1440,suffix:"min")
                RuleStepper(title:"Local save interval",detail:"WPS batches every 10 minutes",value:$interval,range:30...60,suffix:"sec")
                TextField("Work process names, separated by commas",text:$names).textFieldStyle(.roundedBorder)
                HStack { Text("Comma-separated process names").font(.caption); Spacer(); Button("Save rules") { if model.save(input,grace,goal,interval,names) { dismiss() } }.buttonStyle(.borderedProminent) }
            }
        }.onAppear { input=model.inputActivityMinutes; grace=model.workAppGraceMinutes; goal=model.dailyGoalMinutes; interval=model.saveIntervalSeconds; names=model.names }
    }
}

private struct StarMark: View {
    let size: CGFloat

    var body: some View {
        Circle()
            .fill(
                LinearGradient(
                    colors: [FocusTheme.accent, FocusTheme.accentTwo],
                    startPoint: .topLeading,
                    endPoint: .bottomTrailing
                )
            )
            .frame(width: size, height: size)
            .shadow(color: FocusTheme.accent.opacity(0.25), radius: 12, y: 5)
            .overlay {
                Image(systemName: "star.fill")
                    .font(.system(size: size * 0.45, weight: .bold))
                    .foregroundStyle(.white)
            }
    }
}

private struct StatusPill: View {
    let title: String
    let color: Color
    let background: Color

    var body: some View {
        HStack(spacing: 7) {
            StatusDot(color: color)
            Text(title)
                .font(.system(size: 10.5, weight: .bold))
                .tracking(0.7)
        }
        .foregroundStyle(color)
        .padding(.horizontal, 13)
        .padding(.vertical, 8)
        .background(background, in: Capsule())
    }
}

private struct StatusDot: View {
    let color: Color

    var body: some View {
        Circle()
            .fill(color)
            .frame(width: 7, height: 7)
            .shadow(color: color.opacity(0.45), radius: 4)
    }
}

private struct MetricRow: View {
    let title: String
    let value: String
    let color: Color

    var body: some View {
        HStack(spacing: 10) {
            RoundedRectangle(cornerRadius: 3)
                .fill(color)
                .frame(width: 5, height: 34)
            VStack(alignment: .leading, spacing: 2) {
                Text(title)
                    .font(.system(size: 11.5, weight: .semibold))
                    .foregroundStyle(FocusTheme.muted)
                Text(value)
                    .font(.system(size: 20, weight: .bold, design: .rounded))
            }
        }
    }
}

private struct RuleLine: View {
    let label: String
    let value: String

    var body: some View {
        HStack {
            Text(label)
                .font(.system(size: 12.5, weight: .medium))
                .foregroundStyle(FocusTheme.muted)
            Spacer()
            Text(value)
                .font(.system(size: 12.5, weight: .bold))
        }
    }
}

private struct DashboardButton: View {
    let title: String
    let symbol: String
    var isPrimary = false
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Label(title, systemImage: symbol)
                .font(.system(size: 11.5, weight: .bold))
                .frame(maxWidth: .infinity)
                .padding(.vertical, 10)
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .foregroundStyle(isPrimary ? Color.white : FocusTheme.text)
        .background(
            isPrimary
                ? AnyShapeStyle(LinearGradient(colors: [FocusTheme.accent, FocusTheme.accentTwo], startPoint: .leading, endPoint: .trailing))
                : AnyShapeStyle(FocusTheme.background),
            in: RoundedRectangle(cornerRadius: 10, style: .continuous)
        )
    }
}

private struct SheetScaffold<Content: View>: View {
    let title: String
    let subtitle: String
    let symbol: String
    let dismiss: DismissAction
    let content: Content

    init(
        title: String,
        subtitle: String,
        symbol: String,
        dismiss: DismissAction,
        @ViewBuilder content: () -> Content
    ) {
        self.title = title
        self.subtitle = subtitle
        self.symbol = symbol
        self.dismiss = dismiss
        self.content = content()
    }

    var body: some View {
        ZStack {
            FocusTheme.background.ignoresSafeArea()
            VStack(spacing: 20) {
                HStack(spacing: 12) {
                    StarMark(size: 40)
                    VStack(alignment: .leading, spacing: 2) {
                        Text(title)
                            .font(.system(size: 23, weight: .bold, design: .rounded))
                        Text(subtitle)
                            .font(.system(size: 12, weight: .medium))
                            .foregroundStyle(FocusTheme.muted)
                    }
                    Spacer()
                    Button {
                        dismiss()
                    } label: {
                        Image(systemName: "xmark")
                            .font(.system(size: 12, weight: .bold))
                            .frame(width: 30, height: 30)
                            .background(FocusTheme.surface, in: Circle())
                    }
                    .buttonStyle(.plain)
                }

                ScrollView { content }
                Spacer(minLength: 0)
            }
            .padding(24)
        }
        .frame(minWidth: 720, minHeight: 530)
    }
}

private struct SummaryTile: View {
    let label: String
    let value: String
    let detail: String
    let symbol: String
    let color: Color

    var body: some View {
        HStack(spacing: 13) {
            RoundedRectangle(cornerRadius: 12, style: .continuous)
                .fill(color.opacity(0.11))
                .frame(width: 46, height: 46)
                .overlay {
                    Image(systemName: symbol)
                        .foregroundStyle(color)
                }
            VStack(alignment: .leading, spacing: 3) {
                Text(label)
                    .font(.system(size: 9.5, weight: .bold))
                    .tracking(0.7)
                    .foregroundStyle(FocusTheme.muted)
                Text(value)
                    .font(.system(size: 15, weight: .bold, design: .rounded))
                    .lineLimit(1)
                Text(detail)
                    .font(.system(size: 11, weight: .medium))
                    .foregroundStyle(FocusTheme.muted)
            }
            Spacer()
        }
        .padding(16)
        .frame(maxWidth: .infinity)
        .focusCard()
    }
}

private struct AppUsageRow: View {
    let app: AppUsage
    let maximum: Double

    var body: some View {
        HStack(spacing: 14) {
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .fill(app.tint.opacity(0.11))
                .frame(width: 38, height: 38)
                .overlay {
                    Image(systemName: app.symbol)
                        .font(.system(size: 15, weight: .semibold))
                        .foregroundStyle(app.tint)
                }

            VStack(alignment: .leading, spacing: 7) {
                HStack {
                    Text(app.name)
                        .font(.system(size: 13, weight: .bold))
                    Spacer()
                    Text(hourText(app.hours))
                        .font(.system(size: 12.5, weight: .bold))
                }
                GeometryReader { proxy in
                    ZStack(alignment: .leading) {
                        Capsule().fill(FocusTheme.line)
                        Capsule()
                            .fill(LinearGradient(colors: [app.tint, FocusTheme.accentTwo], startPoint: .leading, endPoint: .trailing))
                            .frame(width: proxy.size.width * max(0.04, app.hours / max(maximum, 0.01)))
                    }
                }
                .frame(height: 7)
                Text("Device focus time")
                    .font(.system(size: 10.5, weight: .medium))
                    .foregroundStyle(FocusTheme.muted)
            }
        }
        .padding(.vertical, 13)
    }
}

private struct TransferCard: View {
    let title: String
    let detail: String
    let symbol: String
    let button: String
    let color: Color
    let action: () -> Void

    var body: some View {
        HStack(spacing: 16) {
            RoundedRectangle(cornerRadius: 14, style: .continuous)
                .fill(color.opacity(0.11))
                .frame(width: 56, height: 56)
                .overlay {
                    Image(systemName: symbol)
                        .font(.system(size: 21, weight: .semibold))
                        .foregroundStyle(color)
                }
            VStack(alignment: .leading, spacing: 5) {
                Text(title)
                    .font(.system(size: 16, weight: .bold, design: .rounded))
                Text(detail)
                    .font(.system(size: 12, weight: .medium))
                    .foregroundStyle(FocusTheme.muted)
            }
            Spacer()
            Button(button, action:action)
                .buttonStyle(.borderedProminent)
                .tint(color)
        }
        .padding(20)
        .focusCard()
    }
}

private struct RuleStepper: View {
    let title: String
    let detail: String
    @Binding var value: Int
    let range: ClosedRange<Int>
    var step = 1
    let suffix: String

    var body: some View {
        HStack {
            VStack(alignment: .leading, spacing: 4) {
                Text(title)
                    .font(.system(size: 14, weight: .bold))
                Text(detail)
                    .font(.system(size: 11.5, weight: .medium))
                    .foregroundStyle(FocusTheme.muted)
            }
            Spacer()
            Stepper(value: $value, in: range, step: step) {
                Text("\(value) \(suffix)")
                    .font(.system(size: 12.5, weight: .bold))
                    .frame(width: 82, alignment: .trailing)
            }
            .fixedSize()
        }
        .padding(18)
        .focusCard()
    }
}

private extension View {
    func focusCard() -> some View {
        background(
            FocusTheme.surface,
            in: RoundedRectangle(cornerRadius: 18, style: .continuous)
        )
        .overlay {
            RoundedRectangle(cornerRadius: 18, style: .continuous)
                .stroke(FocusTheme.line.opacity(0.9), lineWidth: 1)
        }
        .shadow(color: Color.black.opacity(0.045), radius: 18, y: 7)
    }

    func sectionLabel() -> some View {
        font(.system(size: 10.5, weight: .bold))
            .tracking(0.85)
            .foregroundStyle(FocusTheme.muted)
    }
}

