using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.CodeAnalysis;
using Microsoft.Win32;

[assembly: Debuggable(DebuggableAttribute.DebuggingModes.Default | DebuggableAttribute.DebuggingModes.DisableOptimizations | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints | DebuggableAttribute.DebuggingModes.EnableEditAndContinue)]
[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: AssemblyVersion("2.4.0.0")]
namespace Microsoft.CodeAnalysis
{
	[CompilerGenerated]
	[Embedded]
	internal sealed class EmbeddedAttribute : Attribute
	{
	}
}
namespace System.Runtime.CompilerServices
{
	[CompilerGenerated]
	[Embedded]
	[AttributeUsage(AttributeTargets.Module, AllowMultiple = false, Inherited = false)]
	internal sealed class RefSafetyRulesAttribute : Attribute
	{
		public readonly int Version;

		public RefSafetyRulesAttribute(int P_0)
		{
			Version = P_0;
		}
	}
}
namespace FocusStar
{
	public sealed class AppSettings
	{
		public int pollIntervalSeconds { get; set; }

		public int inputActiveSeconds { get; set; }

		public int workAppIdleGraceSeconds { get; set; }

		public int dailyGoalMinutes { get; set; }

		public string[] workProcessNames { get; set; }

		public static AppSettings Defaults()
		{
			AppSettings appSettings = new AppSettings();
			appSettings.pollIntervalSeconds = 30;
			appSettings.inputActiveSeconds = 300;
			appSettings.workAppIdleGraceSeconds = 600;
			appSettings.dailyGoalMinutes = 480;
			appSettings.workProcessNames = new string[] { "Code", "WINWORD", "POWERPNT", "wps", "wpp", "et" };
			return appSettings;
		}

		public static AppSettings Load(string path)
		{
			if (!File.Exists(path))
			{
				AppSettings appSettings = Defaults();
				appSettings.Save(path);
				return appSettings;
			}
			try
			{
				using FileStream stream = File.OpenRead(path);
				DataContractJsonSerializer dataContractJsonSerializer = new DataContractJsonSerializer(typeof(AppSettings));
				if (!(dataContractJsonSerializer.ReadObject((Stream)stream) is AppSettings appSettings2))
				{
					return Defaults();
				}
				appSettings2.Normalize();
				return appSettings2;
			}
			catch
			{
				return Defaults();
			}
		}

		public void Normalize()
		{
			pollIntervalSeconds = Math.Max(5, pollIntervalSeconds);
			inputActiveSeconds = Math.Max(60, inputActiveSeconds);
			workAppIdleGraceSeconds = Math.Max(inputActiveSeconds, workAppIdleGraceSeconds);
			dailyGoalMinutes = Math.Max(60, dailyGoalMinutes);
			if (workProcessNames == null)
			{
				workProcessNames = new string[0];
			}
		}

		public void Save(string path)
		{
			Normalize();
			using FileStream stream = File.Create(path);
			new DataContractJsonSerializer(typeof(AppSettings)).WriteObject((Stream)stream, (object)this);
		}
	}
	internal static class NativeMethods
	{
		private struct LASTINPUTINFO
		{
			public uint cbSize;

			public uint dwTime;
		}

		[DllImport("user32.dll")]
		private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

		[DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll")]
		private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

		[DllImport("user32.dll")]
		public static extern bool SetProcessDPIAware();

		public static double GetIdleSeconds()
		{
			LASTINPUTINFO plii = default(LASTINPUTINFO);
			plii.cbSize = (uint)Marshal.SizeOf(plii);
			if (!GetLastInputInfo(ref plii))
			{
				return 0.0;
			}
			return (double)((uint)Environment.TickCount - plii.dwTime) / 1000.0;
		}

		public static string GetForegroundProcessName()
		{
			try
			{
				IntPtr foregroundWindow = GetForegroundWindow();
				if (foregroundWindow == IntPtr.Zero)
				{
					return "";
				}
				GetWindowThreadProcessId(foregroundWindow, out var processId);
				return (processId == 0) ? "" : Process.GetProcessById((int)processId).ProcessName;
			}
			catch
			{
				return "";
			}
		}
	}
	internal static class Theme
	{
		public static readonly Color Background = Color.FromArgb(246, 248, 252);

		public static readonly Color Surface = Color.White;

		public static readonly Color Text = Color.FromArgb(27, 31, 42);

		public static readonly Color Muted = Color.FromArgb(112, 119, 137);

		public static readonly Color Line = Color.FromArgb(230, 233, 241);

		public static readonly Color Accent = Color.FromArgb(83, 91, 242);

		public static readonly Color Accent2 = Color.FromArgb(133, 92, 246);

		public static readonly Color AccentSoft = Color.FromArgb(237, 239, 255);

		public static readonly Color Green = Color.FromArgb(35, 164, 112);

		public static readonly Color GreenSoft = Color.FromArgb(230, 248, 240);

		public static readonly Color Amber = Color.FromArgb(205, 132, 34);

		public static readonly Color AmberSoft = Color.FromArgb(255, 246, 226);
	}
	internal static class Shapes
	{
		public static GraphicsPath RoundRect(Rectangle bounds, int radius)
		{
			int num = radius * 2;
			GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddArc(bounds.Left, bounds.Top, num, num, 180f, 90f);
			graphicsPath.AddArc(bounds.Right - num, bounds.Top, num, num, 270f, 90f);
			graphicsPath.AddArc(bounds.Right - num, bounds.Bottom - num, num, num, 0f, 90f);
			graphicsPath.AddArc(bounds.Left, bounds.Bottom - num, num, num, 90f, 90f);
			graphicsPath.CloseFigure();
			return graphicsPath;
		}

		public static PointF[] Star(RectangleF bounds, int points)
		{
			List<PointF> list = new List<PointF>();
			float num = bounds.Left + bounds.Width / 2f;
			float num2 = bounds.Top + bounds.Height / 2f;
			float num3 = Math.Min(bounds.Width, bounds.Height) / 2f;
			float num4 = num3 * 0.43f;
			for (int i = 0; i < points * 2; i++)
			{
				double num5 = -Math.PI / 2.0 + (double)i * Math.PI / (double)points;
				float num6 = ((i % 2 == 0) ? num3 : num4);
				list.Add(new PointF(num + (float)Math.Cos(num5) * num6, num2 + (float)Math.Sin(num5) * num6));
			}
			return list.ToArray();
		}
	}
	internal static class Ui
	{
		public static Label Label(string text, int x, int y, int width, int height, float size, FontStyle style, Color color)
		{
			Label label = new Label();
			label.Text = text;
			label.Location = new Point(x, y);
			label.Size = new Size(width, height);
			label.Font = new Font("Segoe UI", size, style);
			label.ForeColor = color;
			label.BackColor = Color.Transparent;
			return label;
		}
	}
	internal sealed class CardPanel : Panel
	{
		public int Radius { get; set; }

		public CardPanel()
		{
			Radius = 18;
			BackColor = Color.Transparent;
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.Clear(Theme.Background);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using (GraphicsPath path = Shapes.RoundRect(new Rectangle(2, 4, base.Width - 5, base.Height - 7), Radius))
			{
				using SolidBrush brush = new SolidBrush(Color.FromArgb(13, 25, 39, 74));
				e.Graphics.FillPath(brush, path);
			}
			using (GraphicsPath path2 = Shapes.RoundRect(new Rectangle(0, 0, base.Width - 5, base.Height - 7), Radius))
			{
				using SolidBrush brush2 = new SolidBrush(Theme.Surface);
				using Pen pen = new Pen(Theme.Line);
				e.Graphics.FillPath(brush2, path2);
				e.Graphics.DrawPath(pen, path2);
			}
			base.OnPaint(e);
		}
	}
	internal sealed class ModernButton : Button
	{
		private bool hovering;

		public Color FillColor { get; set; }

		public Color HoverColor { get; set; }

		public Color TextColor { get; set; }

		public int Radius { get; set; }

		public ModernButton()
		{
			base.FlatStyle = FlatStyle.Flat;
			base.FlatAppearance.BorderSize = 0;
			Cursor = Cursors.Hand;
			FillColor = Theme.Accent;
			HoverColor = Color.FromArgb(69, 77, 224);
			TextColor = Color.White;
			Radius = 12;
			Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			hovering = true;
			Invalidate();
			base.OnMouseEnter(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			hovering = false;
			Invalidate();
			base.OnMouseLeave(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Color color = ((base.Parent is CardPanel) ? Theme.Surface : ((base.Parent == null) ? Theme.Background : base.Parent.BackColor));
			if (color.A == 0)
			{
				color = Theme.Background;
			}
			e.Graphics.Clear(color);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle bounds = new Rectangle(0, 0, base.Width - 1, base.Height - 1);
			using (GraphicsPath path = Shapes.RoundRect(bounds, Radius))
			{
				using SolidBrush brush = new SolidBrush(hovering ? HoverColor : FillColor);
				e.Graphics.FillPath(brush, path);
			}
			TextRenderer.DrawText(e.Graphics, Text, Font, bounds, TextColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		}
	}
	internal sealed class StarMark : Control
	{
		public StarMark()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			base.Size = new Size(42, 42);
			BackColor = Color.Transparent;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			RectangleF rect = new RectangleF(0f, 0f, base.Width - 1, base.Height - 1);
			using (LinearGradientBrush brush = new LinearGradientBrush(rect, Theme.Accent, Theme.Accent2, 45f))
			{
				e.Graphics.FillEllipse(brush, rect);
			}
			using (SolidBrush brush2 = new SolidBrush(Color.White))
			{
				e.Graphics.FillPolygon(brush2, Shapes.Star(new RectangleF(11f, 10f, 20f, 20f), 5));
			}
			using SolidBrush brush3 = new SolidBrush(Color.FromArgb(190, 255, 255, 255));
			e.Graphics.FillEllipse(brush3, 29, 8, 5, 5);
		}
	}
	internal sealed class StatusPill : Control
	{
		private string textValue = "FOCUSING";

		private Color color = Theme.Green;

		private Color soft = Theme.GreenSoft;

		public StatusPill()
		{
			base.Size = new Size(118, 34);
			Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		}

		public void SetStatus(string text, Color c, Color s)
		{
			textValue = text;
			color = c;
			soft = s;
			Invalidate();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using (GraphicsPath path = Shapes.RoundRect(new Rectangle(0, 0, base.Width - 1, base.Height - 1), 17))
			{
				using SolidBrush brush = new SolidBrush(soft);
				e.Graphics.FillPath(brush, path);
			}
			using (SolidBrush brush2 = new SolidBrush(color))
			{
				e.Graphics.FillEllipse(brush2, 14, 13, 8, 8);
			}
			TextRenderer.DrawText(e.Graphics, textValue, Font, new Rectangle(29, 0, base.Width - 32, base.Height), color, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		}
	}
	internal sealed class WorkRing : Control
	{
		public double WorkSeconds { get; set; }

		public double GoalSeconds { get; set; }

		public WorkRing()
		{
			base.Size = new Size(122, 122);
			BackColor = Color.White;
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle rect = new Rectangle(10, 10, base.Width - 21, base.Height - 21);
			using (Pen pen = new Pen(Theme.AccentSoft, 10f))
			{
				e.Graphics.DrawArc(pen, rect, -90f, 360f);
			}
			double num = ((GoalSeconds <= 0.0) ? 0.0 : Math.Min(1.0, WorkSeconds / GoalSeconds));
			using (Pen pen2 = new Pen(Theme.Accent, 10f))
			{
				pen2.StartCap = LineCap.Round;
				pen2.EndCap = LineCap.Round;
				e.Graphics.DrawArc(pen2, rect, -90f, (float)(360.0 * num));
			}
			string s = string.Format(CultureInfo.InvariantCulture, "{0:0}%", num * 100.0);
			using Font font = new Font("Segoe UI", 17f, FontStyle.Bold);
			using Font font2 = new Font("Segoe UI", 8f);
			using SolidBrush brush = new SolidBrush(Theme.Text);
			using SolidBrush brush2 = new SolidBrush(Theme.Muted);
			SizeF sizeF = e.Graphics.MeasureString(s, font);
			e.Graphics.DrawString(s, font, brush, ((float)base.Width - sizeF.Width) / 2f, 39f);
			string s2 = "DAILY GOAL";
			SizeF sizeF2 = e.Graphics.MeasureString(s2, font2);
			e.Graphics.DrawString(s2, font2, brush2, ((float)base.Width - sizeF2.Width) / 2f, 69f);
		}
	}
	internal sealed class HistoryPoint
	{
		public string Label;

		public string FullLabel;

		public double Seconds;
	}
	internal sealed class BarChart : Control
	{
		private List<HistoryPoint> points = new List<HistoryPoint>();

		private int hoverIndex = -1;

		private RectangleF ChartArea => new RectangleF(58f, 24f, Math.Max(10, base.Width - 78), Math.Max(10, base.Height - 76));

		public BarChart()
		{
			BackColor = Color.White;
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			base.MouseMove += Moved;
			EventHandler value = delegate
			{
				hoverIndex = -1;
				Invalidate();
			};
			base.MouseLeave += value;
		}

		public void SetPoints(List<HistoryPoint> value)
		{
			points = value ?? new List<HistoryPoint>();
			hoverIndex = -1;
			Invalidate();
		}

		private void Moved(object sender, MouseEventArgs e)
		{
			RectangleF chartArea = ChartArea;
			int num = -1;
			if (points.Count > 0 && chartArea.Contains(e.Location))
			{
				float num2 = chartArea.Width / (float)points.Count;
				num = Math.Max(0, Math.Min(points.Count - 1, (int)(((float)e.X - chartArea.Left) / num2)));
			}
			if (num != hoverIndex)
			{
				hoverIndex = num;
				Invalidate();
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.Clear(Color.White);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			RectangleF chartArea = ChartArea;
			double num = ((points.Count == 0) ? 1.0 : points.Max((HistoryPoint p) => p.Seconds));
			num = Math.Max(3600.0, Math.Ceiling(num / 3600.0) * 3600.0);
			using (Font font = new Font("Segoe UI", 8f))
			{
				using SolidBrush brush = new SolidBrush(Theme.Muted);
				using Pen pen = new Pen(Theme.Line, 1f);
				for (int num2 = 0; num2 <= 4; num2++)
				{
					float num3 = chartArea.Bottom - chartArea.Height * (float)num2 / 4f;
					e.Graphics.DrawLine(pen, chartArea.Left, num3, chartArea.Right, num3);
					string s = string.Format(CultureInfo.InvariantCulture, "{0:0.#}h", num * (double)num2 / 4.0 / 3600.0);
					e.Graphics.DrawString(s, font, brush, 8f, num3 - 7f);
				}
				if (points.Count == 0)
				{
					e.Graphics.DrawString("No history yet", new Font("Segoe UI", 11f), brush, chartArea.Left + 20f, chartArea.Top + chartArea.Height / 2f);
					return;
				}
				float num4 = chartArea.Width / (float)points.Count;
				float num5 = Math.Max(8f, Math.Min(34f, num4 * 0.56f));
				for (int num6 = 0; num6 < points.Count; num6++)
				{
					float num7 = (float)((double)chartArea.Height * points[num6].Seconds / num);
					if (points[num6].Seconds > 0.0 && num7 < 4f)
					{
						num7 = 4f;
					}
					RectangleF rectangleF = new RectangleF(chartArea.Left + (float)num6 * num4 + (num4 - num5) / 2f, chartArea.Bottom - num7, num5, num7);
					Color color = ((num6 == hoverIndex) ? Theme.Accent2 : Theme.Accent);
					using (LinearGradientBrush brush2 = new LinearGradientBrush((rectangleF.Height < 1f) ? new RectangleF(rectangleF.X, chartArea.Bottom - 1f, rectangleF.Width, 1f) : rectangleF, color, Theme.Accent2, 90f))
					{
						using GraphicsPath path = Shapes.RoundRect(Rectangle.Round(rectangleF), Math.Min(7, Math.Max(1, (int)(num5 / 3f))));
						if (num7 > 0f)
						{
							e.Graphics.FillPath(brush2, path);
						}
					}
					SizeF sizeF = e.Graphics.MeasureString(points[num6].Label, font);
					e.Graphics.DrawString(points[num6].Label, font, brush, chartArea.Left + (float)num6 * num4 + (num4 - sizeF.Width) / 2f, chartArea.Bottom + 12f);
				}
			}
			if (hoverIndex < 0 || hoverIndex >= points.Count)
			{
				return;
			}
			HistoryPoint historyPoint = points[hoverIndex];
			string text = historyPoint.FullLabel + "  •  " + ActivityRepository.FormatDuration(historyPoint.Seconds);
			using Font font2 = new Font("Segoe UI", 8.5f, FontStyle.Bold);
			int num8 = (int)e.Graphics.MeasureString(text, font2).Width + 24;
			int num9 = Math.Max(60, Math.Min(base.Width - num8 - 10, PointToClient(Cursor.Position).X - num8 / 2));
			Rectangle bounds = new Rectangle(num9, 4, num8, 30);
			using (GraphicsPath path2 = Shapes.RoundRect(bounds, 9))
			{
				using SolidBrush brush3 = new SolidBrush(Theme.Text);
				e.Graphics.FillPath(brush3, path2);
			}
			TextRenderer.DrawText(e.Graphics, text, font2, bounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		}
	}
	internal sealed class AppUsageStat
	{
		public string ProcessName;

		public double Seconds;

		public int Sessions;

		public bool IsWorkApp;
	}
	internal enum UsageRange
	{
		Today,
		SevenDays,
		ThirtyDays,
		AllTime
	}
	internal sealed class UsageRowControl : Control
	{
		private readonly AppUsageStat stat;

		private readonly double maximum;

		public UsageRowControl(AppUsageStat value, double max)
		{
			stat = value;
			maximum = Math.Max(1.0, max);
			base.Size = new Size(790, 62);
			base.BackColor = Color.White;
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.Clear(Color.White);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using (Font font = new Font("Segoe UI", 10f, FontStyle.Bold))
			{
				using Font font2 = new Font("Segoe UI", 8.5f);
				using SolidBrush brush = new SolidBrush(Theme.Text);
				using SolidBrush brush2 = new SolidBrush(Theme.Muted);
				e.Graphics.DrawString(stat.ProcessName, font, brush, 2f, 4f);
				string s = string.Format(CultureInfo.InvariantCulture, "{0} recorded intervals", stat.Sessions);
				e.Graphics.DrawString(s, font2, brush2, 2f, 29f);
				string s2 = ActivityRepository.FormatDuration(stat.Seconds);
				SizeF sizeF = e.Graphics.MeasureString(s2, font);
				e.Graphics.DrawString(s2, font, brush, (float)base.Width - sizeF.Width - 10f, 4f);
			}
			Rectangle bounds = new Rectangle(170, 35, base.Width - 190, 10);
			using (GraphicsPath path = Shapes.RoundRect(bounds, 5))
			{
				using SolidBrush brush3 = new SolidBrush(Theme.AccentSoft);
				e.Graphics.FillPath(brush3, path);
			}
			int val = Math.Max(5, (int)((double)bounds.Width * stat.Seconds / maximum));
			Rectangle rectangle = new Rectangle(bounds.X, bounds.Y, Math.Min(bounds.Width, val), bounds.Height);
			using (GraphicsPath path2 = Shapes.RoundRect(rectangle, 5))
			{
				using LinearGradientBrush brush4 = new LinearGradientBrush(rectangle, Theme.Accent, Theme.Accent2, 0f);
				e.Graphics.FillPath(brush4, path2);
			}
			using Pen pen = new Pen(Theme.Line);
			e.Graphics.DrawLine(pen, 0, base.Height - 1, base.Width, base.Height - 1);
		}
	}
	internal sealed class AppUsageForm : Form
	{
		private readonly ActivityRepository repository;

		private readonly AppSettings settings;

		private readonly Dictionary<UsageRange, ModernButton> buttons = new Dictionary<UsageRange, ModernButton>();

		private readonly Label mostUsedValue;

		private readonly Label frequentValue;

		private readonly Label trackedValue;

		private readonly Label listTitle;

		private readonly FlowLayoutPanel rows;

		public AppUsageForm(ActivityRepository repo, AppSettings currentSettings)
		{
			repository = repo;
			settings = currentSettings;
			Text = "App usage";
			BackColor = Theme.Background;
			base.ClientSize = new Size(920, 650);
			base.FormBorderStyle = FormBorderStyle.FixedSingle;
			base.MaximizeBox = false;
			base.StartPosition = FormStartPosition.CenterParent;
			base.ShowIcon = false;
			Font = new Font("Segoe UI", 9f);
			base.Controls.Add(new StarMark
			{
				Location = new Point(32, 25)
			});
			base.Controls.Add(Ui.Label("App usage", 86, 23, 350, 36, 20f, FontStyle.Bold, Theme.Text));
			base.Controls.Add(Ui.Label("See where your focused time goes", 88, 59, 390, 24, 9f, FontStyle.Regular, Theme.Muted));
			AddRangeButton("Today", UsageRange.Today, 520, 30, 78);
			AddRangeButton("7 days", UsageRange.SevenDays, 606, 30, 78);
			AddRangeButton("30 days", UsageRange.ThirtyDays, 692, 30, 82);
			AddRangeButton("All time", UsageRange.AllTime, 782, 30, 90);
			AddMetric("MOST USED", out mostUsedValue, 32, 105);
			AddMetric("MOST RECORDED WORK APP", out frequentValue, 327, 105);
			AddMetric("APPS TRACKED", out trackedValue, 622, 105);
			CardPanel cardPanel = new CardPanel
			{
				Location = new Point(32, 220),
				Size = new Size(856, 378)
			};
			base.Controls.Add(cardPanel);
			listTitle = Ui.Label("Usage by app", 25, 20, 300, 26, 11f, FontStyle.Bold, Theme.Text);
			cardPanel.Controls.Add(listTitle);
			rows = new FlowLayoutPanel
			{
				Location = new Point(22, 55),
				Size = new Size(807, 287),
				FlowDirection = FlowDirection.TopDown,
				WrapContents = false,
				AutoScroll = true,
				BackColor = Color.White
			};
			cardPanel.Controls.Add(rows);
			base.Controls.Add(Ui.Label("Device/app details are separate. Interval counts are not app-launch counts. Shared Mac whitelist.", 36, 612, 700, 22, 8.5f, FontStyle.Regular, Theme.Muted));
			SelectRange(UsageRange.Today);
		}

		private void AddMetric(string title, out Label value, int x, int y)
		{
			CardPanel cardPanel = new CardPanel();
			cardPanel.Location = new Point(x, y);
			cardPanel.Size = new Size(266, 92);
			CardPanel cardPanel2 = cardPanel;
			base.Controls.Add(cardPanel2);
			cardPanel2.Controls.Add(Ui.Label(title, 22, 17, 220, 20, 8f, FontStyle.Bold, Theme.Muted));
			value = Ui.Label("—", 21, 40, 220, 31, 14f, FontStyle.Bold, Theme.Text);
			value.AutoEllipsis = true;
			cardPanel2.Controls.Add(value);
		}

		private void AddRangeButton(string text, UsageRange range, int x, int y, int width)
		{
			ModernButton modernButton = new ModernButton();
			modernButton.Text = text;
			modernButton.Location = new Point(x, y);
			modernButton.Size = new Size(width, 36);
			modernButton.FillColor = Color.White;
			modernButton.HoverColor = Theme.AccentSoft;
			modernButton.TextColor = Theme.Muted;
			modernButton.Radius = 10;
			ModernButton modernButton2 = modernButton;
			modernButton2.Click += delegate
			{
				SelectRange(range);
			};
			buttons[range] = modernButton2;
			base.Controls.Add(modernButton2);
		}

		private void SelectRange(UsageRange range)
		{
			foreach (KeyValuePair<UsageRange, ModernButton> button in buttons)
			{
				bool flag = button.Key == range;
				button.Value.FillColor = (flag ? Theme.Accent : Color.White);
				button.Value.TextColor = (flag ? Color.White : Theme.Muted);
				button.Value.Invalidate();
			}
			DateTime? startDate = null;
			switch (range)
			{
			case UsageRange.Today:
				startDate = ActivityRepository.Today;
				break;
			case UsageRange.SevenDays:
				startDate = ActivityRepository.Today.AddDays(-6.0);
				break;
			case UsageRange.ThirtyDays:
				startDate = ActivityRepository.Today.AddDays(-29.0);
				break;
			}
			List<AppUsageStat> list = repository.LoadAppUsage(startDate, settings.workProcessNames);
			AppUsageStat appUsageStat = list.FirstOrDefault();
			AppUsageStat appUsageStat2 = (from value in list
				where value.IsWorkApp
				orderby value.Sessions descending, value.Seconds descending
				select value).FirstOrDefault();
			mostUsedValue.Text = ((appUsageStat == null) ? "—" : appUsageStat.ProcessName);
			frequentValue.Text = ((appUsageStat2 == null) ? "—" : string.Format(CultureInfo.InvariantCulture, "{0} · {1}×", appUsageStat2.ProcessName, appUsageStat2.Sessions));
			trackedValue.Text = list.Count.ToString(CultureInfo.InvariantCulture);
			Label label = listTitle;
			Label label2 = label;
			label2.Text = range switch
			{
				UsageRange.ThirtyDays => "Usage in the last 30 days", 
				UsageRange.SevenDays => "Usage in the last 7 days", 
				UsageRange.Today => "Usage today", 
				_ => "All-time usage", 
			};
			rows.SuspendLayout();
			rows.Controls.Clear();
			double max = ((list.Count == 0) ? 1.0 : list.Max((AppUsageStat value) => value.Seconds));
			foreach (AppUsageStat item in list)
			{
				rows.Controls.Add(new UsageRowControl(item, max));
			}
			if (list.Count == 0)
			{
				rows.Controls.Add(Ui.Label("No app usage has been recorded in this period.", 0, 0, 600, 50, 10f, FontStyle.Regular, Theme.Muted));
			}
			rows.ResumeLayout();
		}
	}
	internal sealed class DataTransferForm : Form
	{
		private readonly ActivityRepository repository;

		public DataTransferForm(ActivityRepository repo)
		{
			repository = repo;
			Text = "Data transfer";
			BackColor = Theme.Background;
			base.ClientSize = new Size(650, 440);
			base.FormBorderStyle = FormBorderStyle.FixedDialog;
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.StartPosition = FormStartPosition.CenterParent;
			base.ShowIcon = false;
			base.Controls.Add(new StarMark
			{
				Location = new Point(28, 24)
			});
			base.Controls.Add(Ui.Label("Data transfer", 82, 22, 360, 36, 20f, FontStyle.Bold, Theme.Text));
			base.Controls.Add(Ui.Label("Move your Focus Star history between devices", 84, 58, 430, 24, 9f, FontStyle.Regular, Theme.Muted));
			CardPanel cardPanel = new CardPanel
			{
				Location = new Point(28, 100),
				Size = new Size(594, 82)
			};
			base.Controls.Add(cardPanel);
			cardPanel.Controls.Add(Ui.Label("WPS SHARED DATA", 22, 16, 180, 20, 8f, FontStyle.Bold, Theme.Accent));
			cardPanel.Controls.Add(Ui.Label("Download data and mac-data on both devices. Statistics refresh every 30 seconds.", 22, 39, 540, 28, 9f, FontStyle.Regular, Theme.Text));
			CardPanel cardPanel2 = new CardPanel
			{
				Location = new Point(28, 202),
				Size = new Size(284, 182)
			};
			base.Controls.Add(cardPanel2);
			cardPanel2.Controls.Add(Ui.Label("Export backup", 22, 20, 220, 28, 12f, FontStyle.Bold, Theme.Text));
			cardPanel2.Controls.Add(Ui.Label("Create one .focusstar file containing settings and all history.", 22, 55, 230, 48, 9f, FontStyle.Regular, Theme.Muted));
			ModernButton modernButton = new ModernButton
			{
				Text = "Export",
				Location = new Point(22, 117),
				Size = new Size(234, 40)
			};
			modernButton.Click += ExportClicked;
			cardPanel2.Controls.Add(modernButton);
			CardPanel cardPanel3 = new CardPanel
			{
				Location = new Point(338, 202),
				Size = new Size(284, 182)
			};
			base.Controls.Add(cardPanel3);
			cardPanel3.Controls.Add(Ui.Label("Import backup", 22, 20, 220, 28, 12f, FontStyle.Bold, Theme.Text));
			cardPanel3.Controls.Add(Ui.Label("Add missing files. Existing conflicting history is never overwritten.", 22, 55, 230, 48, 9f, FontStyle.Regular, Theme.Muted));
			ModernButton modernButton2 = new ModernButton
			{
				Text = "Import",
				Location = new Point(22, 117),
				Size = new Size(234, 40),
				FillColor = Theme.AccentSoft,
				HoverColor = Color.FromArgb(226, 229, 255),
				TextColor = Theme.Accent
			};
			modernButton2.Click += ImportClicked;
			cardPanel3.Controls.Add(modernButton2);
			base.Controls.Add(Ui.Label("Backups are ordinary local files. Save them in OneDrive, Dropbox, or another cloud folder you trust.", 32, 402, 590, 24, 8.5f, FontStyle.Regular, Theme.Muted));
		}

		private void ExportClicked(object sender, EventArgs e)
		{
			using SaveFileDialog saveFileDialog = new SaveFileDialog();
			saveFileDialog.Title = "Export Focus Star backup";
			saveFileDialog.Filter = "Focus Star backup (*.focusstar)|*.focusstar";
			saveFileDialog.DefaultExt = "focusstar";
			saveFileDialog.FileName = "FocusStar-" + ActivityRepository.Today.ToString("yyyy-MM-dd") + ".focusstar";
			if (saveFileDialog.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}
			try
			{
				repository.ExportBackup(saveFileDialog.FileName);
				MessageBox.Show(this, "Backup exported successfully.", "Focus Star", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "Could not export the backup.\r\n\r\n" + ex.Message, "Focus Star", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		}

		private void ImportClicked(object sender, EventArgs e)
		{
			using OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Title = "Import Focus Star backup";
			openFileDialog.Filter = "Focus Star backup (*.focusstar)|*.focusstar|All files (*.*)|*.*";
			if (openFileDialog.ShowDialog(this) != DialogResult.OK || MessageBox.Show(this, "Add missing Windows/Mac history and settings? Existing conflicting files will stop the import without overwriting them.", "Import backup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
			{
				return;
			}
			try
			{
				repository.ImportBackup(openFileDialog.FileName);
				MessageBox.Show(this, "Backup imported successfully.", "Focus Star", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				base.DialogResult = DialogResult.OK;
				Close();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "Could not import the backup.\r\n\r\n" + ex.Message, "Focus Star", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		}
	}
	internal enum HistoryRange
	{
		Daily,
		Weekly,
		Monthly,
		Yearly
	}
	internal sealed class HistoryForm : Form
	{
		private readonly ActivityRepository repository;

		private readonly Dictionary<HistoryRange, ModernButton> rangeButtons = new Dictionary<HistoryRange, ModernButton>();

		private readonly BarChart chart;

		private readonly Label totalValue;

		private readonly Label averageValue;

		private readonly Label bestValue;

		private readonly Label chartTitle;

		private HistoryRange currentRange;

		public HistoryForm(ActivityRepository repo)
		{
			repository = repo;
			Text = "Focus history";
			BackColor = Theme.Background;
			base.ClientSize = new Size(920, 620);
			base.FormBorderStyle = FormBorderStyle.FixedSingle;
			base.MaximizeBox = false;
			base.StartPosition = FormStartPosition.CenterParent;
			base.ShowIcon = false;
			Font = new Font("Segoe UI", 9f);
			base.Controls.Add(new StarMark
			{
				Location = new Point(32, 25)
			});
			base.Controls.Add(Ui.Label("Focus history", 86, 23, 350, 36, 20f, FontStyle.Bold, Theme.Text));
			base.Controls.Add(Ui.Label("See how your focus compounds over time", 88, 59, 390, 24, 9f, FontStyle.Regular, Theme.Muted));
			int num = 548;
			AddRangeButton("Daily", HistoryRange.Daily, num, 30);
			AddRangeButton("Weekly", HistoryRange.Weekly, num + 82, 30);
			AddRangeButton("Monthly", HistoryRange.Monthly, num + 164, 30);
			AddRangeButton("Yearly", HistoryRange.Yearly, num + 246, 30);
			AddMetric("TOTAL FOCUS", out totalValue, 32, 105);
			AddMetric("AVERAGE", out averageValue, 327, 105);
			AddMetric("BEST PERIOD", out bestValue, 622, 105);
			CardPanel cardPanel = new CardPanel
			{
				Location = new Point(32, 220),
				Size = new Size(856, 354)
			};
			base.Controls.Add(cardPanel);
			chartTitle = Ui.Label("Last 14 days", 25, 20, 300, 26, 11f, FontStyle.Bold, Theme.Text);
			cardPanel.Controls.Add(chartTitle);
			chart = new BarChart
			{
				Location = new Point(20, 55),
				Size = new Size(811, 265)
			};
			cardPanel.Controls.Add(chart);
			base.Controls.Add(Ui.Label(string.IsNullOrEmpty(repository.Warning) ? "Deduplicated focus · UTC+08:00 · Hover for duration" : repository.Warning, 36, 584, 825, 22, 8.5f, FontStyle.Regular, Theme.Muted));
			SelectRange(HistoryRange.Daily);
		}

		private void AddMetric(string title, out Label value, int x, int y)
		{
			CardPanel cardPanel = new CardPanel();
			cardPanel.Location = new Point(x, y);
			cardPanel.Size = new Size(266, 92);
			CardPanel cardPanel2 = cardPanel;
			base.Controls.Add(cardPanel2);
			cardPanel2.Controls.Add(Ui.Label(title, 22, 17, 210, 20, 8f, FontStyle.Bold, Theme.Muted));
			value = Ui.Label("0h 0m", 21, 40, 220, 31, 16f, FontStyle.Bold, Theme.Text);
			cardPanel2.Controls.Add(value);
		}

		private void AddRangeButton(string text, HistoryRange range, int x, int y)
		{
			ModernButton modernButton = new ModernButton();
			modernButton.Text = text;
			modernButton.Location = new Point(x, y);
			modernButton.Size = new Size(74, 36);
			modernButton.FillColor = Color.White;
			modernButton.HoverColor = Theme.AccentSoft;
			modernButton.TextColor = Theme.Muted;
			modernButton.Radius = 10;
			ModernButton modernButton2 = modernButton;
			modernButton2.Click += delegate
			{
				SelectRange(range);
			};
			rangeButtons[range] = modernButton2;
			base.Controls.Add(modernButton2);
		}

		private void SelectRange(HistoryRange range)
		{
			currentRange = range;
			foreach (KeyValuePair<HistoryRange, ModernButton> rangeButton in rangeButtons)
			{
				bool flag = rangeButton.Key == currentRange;
				rangeButton.Value.FillColor = (flag ? Theme.Accent : Color.White);
				rangeButton.Value.TextColor = (flag ? Color.White : Theme.Muted);
				rangeButton.Value.Invalidate();
			}
			List<HistoryPoint> list = BuildPoints(range);
			chart.SetPoints(list);
			double num = list.Sum((HistoryPoint p) => p.Seconds);
			double seconds = ((list.Count == 0) ? 0.0 : (num / (double)list.Count));
			HistoryPoint historyPoint = list.OrderByDescending((HistoryPoint p) => p.Seconds).FirstOrDefault();
			totalValue.Text = ActivityRepository.FormatDuration(num);
			averageValue.Text = ActivityRepository.FormatDuration(seconds);
			bestValue.Text = ((historyPoint == null || historyPoint.Seconds <= 0.0) ? "—" : historyPoint.FullLabel);
			Label label = chartTitle;
			Label label2 = label;
			label2.Text = range switch
			{
				HistoryRange.Monthly => "Last 12 months", 
				HistoryRange.Weekly => "Last 12 weeks", 
				HistoryRange.Daily => "Last 14 days", 
				_ => "Last 6 years", 
			};
		}

		private List<HistoryPoint> BuildPoints(HistoryRange range)
		{
			SortedDictionary<DateTime, double> sortedDictionary = repository.LoadAllWorkByDay();
			DateTime today = ActivityRepository.Today;
			List<HistoryPoint> list = new List<HistoryPoint>();
			switch (range)
			{
			case HistoryRange.Daily:
			{
				for (int num4 = 13; num4 >= 0; num4--)
				{
					DateTime key = today.AddDays(-num4);
					list.Add(new HistoryPoint
					{
						Label = key.ToString("M/d"),
						FullLabel = key.ToString("MMM d"),
						Seconds = (sortedDictionary.ContainsKey(key) ? sortedDictionary[key] : 0.0)
					});
				}
				break;
			}
			case HistoryRange.Weekly:
			{
				DateTime dateTime = today.AddDays(-((int)(today.DayOfWeek + 6) % 7));
				for (int num2 = 11; num2 >= 0; num2--)
				{
					DateTime start = dateTime.AddDays(-7 * num2);
					double seconds2 = sortedDictionary.Where((KeyValuePair<DateTime, double> p) => p.Key >= start && p.Key < start.AddDays(7.0)).Sum((KeyValuePair<DateTime, double> p) => p.Value);
					list.Add(new HistoryPoint
					{
						Label = start.ToString("M/d"),
						FullLabel = "Week of " + start.ToString("MMM d"),
						Seconds = seconds2
					});
				}
				break;
			}
			case HistoryRange.Monthly:
			{
				DateTime dateTime2 = new DateTime(today.Year, today.Month, 1);
				for (int num3 = 11; num3 >= 0; num3--)
				{
					DateTime month = dateTime2.AddMonths(-num3);
					double seconds3 = sortedDictionary.Where((KeyValuePair<DateTime, double> p) => p.Key.Year == month.Year && p.Key.Month == month.Month).Sum((KeyValuePair<DateTime, double> p) => p.Value);
					list.Add(new HistoryPoint
					{
						Label = month.ToString("MMM yy"),
						FullLabel = month.ToString("MMMM yyyy"),
						Seconds = seconds3
					});
				}
				break;
			}
			default:
			{
				for (int num = 5; num >= 0; num--)
				{
					int year = today.Year - num;
					double seconds = sortedDictionary.Where((KeyValuePair<DateTime, double> p) => p.Key.Year == year).Sum((KeyValuePair<DateTime, double> p) => p.Value);
					list.Add(new HistoryPoint
					{
						Label = year.ToString(),
						FullLabel = year.ToString(),
						Seconds = seconds
					});
				}
				break;
			}
			}
			return list;
		}
	}
	internal sealed class SettingsForm : Form
	{
		private readonly string path;

		private readonly NumericUpDown inputMinutes;

		private readonly NumericUpDown appMinutes;

		private readonly NumericUpDown pollSeconds;

		private readonly NumericUpDown goalHours;

		private readonly TextBox processes;

		public SettingsForm(string settingsPath, AppSettings settings)
		{
			path = settingsPath;
			Text = "Tracking settings";
			BackColor = Theme.Background;
			base.ClientSize = new Size(520, 570);
			base.FormBorderStyle = FormBorderStyle.FixedDialog;
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.StartPosition = FormStartPosition.CenterParent;
			base.ShowIcon = false;
			base.Controls.Add(Ui.Label("Tracking settings", 24, 22, 330, 32, 17f, FontStyle.Bold, Theme.Text));
			base.Controls.Add(Ui.Label("Changes apply immediately and stay on this computer.", 26, 56, 450, 24, 9f, FontStyle.Regular, Theme.Muted));
			CardPanel cardPanel = new CardPanel
			{
				Location = new Point(24, 96),
				Size = new Size(472, 398)
			};
			base.Controls.Add(cardPanel);
			cardPanel.Controls.Add(Ui.Label("Input activity window", 26, 24, 220, 24, 9f, FontStyle.Bold, Theme.Text));
			inputMinutes = Number(settings.inputActiveSeconds / 60, 1m, 60m, 268, 20);
			cardPanel.Controls.Add(inputMinutes);
			cardPanel.Controls.Add(Ui.Label("min", 370, 24, 55, 24, 9f, FontStyle.Regular, Theme.Muted));
			cardPanel.Controls.Add(Ui.Label("Work app reading grace", 26, 74, 220, 24, 9f, FontStyle.Bold, Theme.Text));
			appMinutes = Number(settings.workAppIdleGraceSeconds / 60, 1m, 180m, 268, 70);
			cardPanel.Controls.Add(appMinutes);
			cardPanel.Controls.Add(Ui.Label("min", 370, 74, 55, 24, 9f, FontStyle.Regular, Theme.Muted));
			cardPanel.Controls.Add(Ui.Label("Save interval", 26, 124, 220, 24, 9f, FontStyle.Bold, Theme.Text));
			pollSeconds = Number(settings.pollIntervalSeconds, 5m, 300m, 268, 120);
			cardPanel.Controls.Add(pollSeconds);
			cardPanel.Controls.Add(Ui.Label("sec", 370, 124, 55, 24, 9f, FontStyle.Regular, Theme.Muted));
			cardPanel.Controls.Add(Ui.Label("Daily goal", 26, 174, 220, 24, 9f, FontStyle.Bold, Theme.Text));
			goalHours = Number(Math.Max(1, settings.dailyGoalMinutes / 60), 1m, 16m, 268, 170);
			cardPanel.Controls.Add(goalHours);
			cardPanel.Controls.Add(Ui.Label("hours", 370, 174, 55, 24, 9f, FontStyle.Regular, Theme.Muted));
			cardPanel.Controls.Add(Ui.Label("Work process names (one per line)", 26, 224, 390, 24, 9f, FontStyle.Bold, Theme.Text));
			processes = new TextBox
			{
				Location = new Point(26, 254),
				Size = new Size(410, 112),
				Multiline = true,
				ScrollBars = ScrollBars.Vertical,
				BorderStyle = BorderStyle.FixedSingle,
				Font = new Font("Consolas", 9f),
				Text = string.Join(Environment.NewLine, settings.workProcessNames ?? new string[0])
			};
			cardPanel.Controls.Add(processes);
			ModernButton modernButton = new ModernButton
			{
				Text = "Cancel",
				Location = new Point(298, 512),
				Size = new Size(94, 40),
				FillColor = Color.White,
				HoverColor = Theme.AccentSoft,
				TextColor = Theme.Text
			};
			modernButton.Click += delegate
			{
				base.DialogResult = DialogResult.Cancel;
				Close();
			};
			base.Controls.Add(modernButton);
			ModernButton modernButton2 = new ModernButton
			{
				Text = "Save",
				Location = new Point(400, 512),
				Size = new Size(96, 40)
			};
			modernButton2.Click += Save;
			base.Controls.Add(modernButton2);
		}

		private static NumericUpDown Number(decimal value, decimal min, decimal max, int x, int y)
		{
			NumericUpDown numericUpDown = new NumericUpDown();
			numericUpDown.Location = new Point(x, y);
			numericUpDown.Size = new Size(92, 28);
			numericUpDown.Minimum = min;
			numericUpDown.Maximum = max;
			numericUpDown.Value = Math.Max(min, Math.Min(max, value));
			numericUpDown.BorderStyle = BorderStyle.FixedSingle;
			numericUpDown.Font = new Font("Segoe UI", 10f);
			numericUpDown.TextAlign = HorizontalAlignment.Center;
			return numericUpDown;
		}

		private void Save(object sender, EventArgs e)
		{
			string[] workProcessNames = (from line in processes.Lines
				select line.Trim() into line
				where line.Length > 0
				select line).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
			AppSettings appSettings = new AppSettings();
			appSettings.pollIntervalSeconds = (int)pollSeconds.Value;
			appSettings.inputActiveSeconds = (int)inputMinutes.Value * 60;
			appSettings.workAppIdleGraceSeconds = (int)appMinutes.Value * 60;
			appSettings.dailyGoalMinutes = (int)goalHours.Value * 60;
			appSettings.workProcessNames = workProcessNames;
			appSettings.Save(path);
			base.DialogResult = DialogResult.OK;
			Close();
		}
	}
	internal sealed class MainForm : Form
	{
		private readonly string root;

		private readonly string settingsPath;

		private AppSettings settings;

		private readonly ActivityRepository repository;

		private readonly System.Windows.Forms.Timer timer;

		private DateTime lastTick;

		private DateTime lastSave;

		private string currentDate;

		private string state;

		private string reason;

		private string process;

		private double idle;

		private double work;

		private double inactive;

		private double segment;

		private bool paused;

		private Label timeLabel;

		private Label workLabel;

		private Label inactiveLabel;

		private Label appLabel;

		private Label idleLabel;

		private Label rulesLabel;

		private StatusPill status;

		private WorkRing ring;

		private ModernButton pauseButton;

		private NotifyIcon trayIcon;

		private ContextMenuStrip trayMenu;

		private bool exitRequested;

		private readonly bool startHidden;

		private bool trayHintShown;

		private bool suspended;

		private DateTime segmentEnd;

		private DateTime lastRefreshRequest = DateTime.MinValue;

		private DateTime lastSharedFlush;

		private Label syncLabel;

		private Label deviceLabel;

		private string recordingError = "";

		public MainForm(string rootDirectory, bool launchInBackground, bool preview = false)
		{
			paused = preview;
			startHidden = launchInBackground;
			root = rootDirectory;
			settingsPath = Path.Combine(root, "settings.json");
			settings = AppSettings.Load(settingsPath);
			repository = new ActivityRepository(root);
			repository.SetWorkProcessNames(settings.workProcessNames);
			try
			{
				repository.FlushPendingToShared();
			}
			catch (Exception ex)
			{
				recordingError = "Local recovery pending: " + ex.Message;
				LogError(ex);
			}
			repository.RefreshAsync();
			currentDate = ActivityRepository.Today.ToString("yyyy-MM-dd");
			repository.LoadDay(currentDate, out work, out inactive);
			Text = "Focus Star";
			BackColor = Theme.Background;
			base.ClientSize = new Size(900, 580);
			base.FormBorderStyle = FormBorderStyle.FixedSingle;
			base.MaximizeBox = false;
			base.StartPosition = FormStartPosition.CenterScreen;
			Font = new Font("Segoe UI", 9f);
			base.AutoScaleMode = AutoScaleMode.Dpi;
			try
			{
				base.Icon = new Icon(Path.Combine(root, "FocusStar.ico"));
			}
			catch
			{
				base.ShowIcon = false;
			}
			BuildUi();
			if (preview)
			{
				pauseButton.Enabled = false;
				pauseButton.Text = "Preview";
			}
			BuildTrayIcon();
			lastTick = (lastSave = (lastSharedFlush = DateTime.Now));
			Evaluate();
			UpdateUi();
			timer = new System.Windows.Forms.Timer
			{
				Interval = 1000
			};
			timer.Tick += Tick;
			timer.Start();
			SystemEvents.PowerModeChanged += PowerChanged;
			SystemEvents.SessionSwitch += SessionChanged;
			base.FormClosing += Closing;
			base.Shown += delegate
			{
				if (startHidden)
				{
					BeginInvoke(new Action(HideToTray));
				}
			};
		}

		private void BuildTrayIcon()
		{
			ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem("Open Focus Star");
			toolStripMenuItem.Font = new Font(toolStripMenuItem.Font, FontStyle.Bold);
			toolStripMenuItem.Click += delegate
			{
				ShowFromTray();
			};
			ToolStripMenuItem toolStripMenuItem2 = new ToolStripMenuItem("Exit Focus Star");
			toolStripMenuItem2.Click += delegate
			{
				exitRequested = true;
				Close();
			};
			trayMenu = new ContextMenuStrip();
			trayMenu.Items.Add(toolStripMenuItem);
			trayMenu.Items.Add(new ToolStripSeparator());
			trayMenu.Items.Add(toolStripMenuItem2);
			trayIcon = new NotifyIcon();
			trayIcon.Text = "Focus Star — tracking in background";
			trayIcon.Icon = base.Icon ?? SystemIcons.Application;
			trayIcon.ContextMenuStrip = trayMenu;
			trayIcon.Visible = true;
			trayIcon.DoubleClick += delegate
			{
				ShowFromTray();
			};
		}

		private void HideToTray()
		{
			Hide();
			base.ShowInTaskbar = false;
		}

		private void ShowFromTray()
		{
			base.ShowInTaskbar = true;
			Show();
			if (base.WindowState == FormWindowState.Minimized)
			{
				base.WindowState = FormWindowState.Normal;
			}
			Activate();
			BringToFront();
		}

		private void BuildUi()
		{
			base.Controls.Add(new StarMark
			{
				Location = new Point(32, 24)
			});
			base.Controls.Add(Ui.Label("Focus Star", 86, 22, 300, 36, 20f, FontStyle.Bold, Theme.Text));
			base.Controls.Add(Ui.Label("Quietly tracking the time you show up", 88, 58, 390, 24, 9f, FontStyle.Regular, Theme.Muted));
			status = new StatusPill
			{
				Location = new Point(748, 30)
			};
			base.Controls.Add(status);
			CardPanel cardPanel = new CardPanel();
			cardPanel.Location = new Point(32, 101);
			cardPanel.Size = new Size(525, 382);
			CardPanel cardPanel2 = cardPanel;
			base.Controls.Add(cardPanel2);
			cardPanel2.Controls.Add(Ui.Label("TODAY'S FOCUS", 28, 25, 180, 24, 8.5f, FontStyle.Bold, Theme.Muted));
			timeLabel = Ui.Label("0h 0m", 27, 52, 315, 63, 34f, FontStyle.Bold, Theme.Text);
			cardPanel2.Controls.Add(timeLabel);
			cardPanel2.Controls.Add(Ui.Label("GOAL PROGRESS", 29, 119, 130, 23, 8f, FontStyle.Bold, Theme.Muted));
			ring = new WorkRing
			{
				Location = new Point(374, 23)
			};
			cardPanel2.Controls.Add(ring);
			cardPanel2.Controls.Add(new Panel
			{
				Location = new Point(28, 157),
				Size = new Size(465, 1),
				BackColor = Theme.Line
			});
			cardPanel2.Controls.Add(Ui.Label("ALL-TIME FOCUS", 29, 181, 170, 22, 8f, FontStyle.Bold, Theme.Muted));
			workLabel = Ui.Label("0h 0m", 29, 207, 185, 35, 16f, FontStyle.Bold, Theme.Text);
			cardPanel2.Controls.Add(workLabel);
			cardPanel2.Controls.Add(Ui.Label("WINDOWS INACTIVE", 267, 181, 180, 22, 8f, FontStyle.Bold, Theme.Muted));
			inactiveLabel = Ui.Label("0h 0m", 267, 207, 185, 35, 16f, FontStyle.Bold, Theme.Text);
			cardPanel2.Controls.Add(inactiveLabel);
			deviceLabel = Ui.Label("Windows: —   Mac: —", 29, 255, 465, 28, 9f, FontStyle.Regular, Theme.Muted);
			cardPanel2.Controls.Add(deviceLabel);
			pauseButton = new ModernButton
			{
				Text = "Pause",
				Location = new Point(28, 299),
				Size = new Size(107, 48)
			};
			pauseButton.Click += TogglePause;
			cardPanel2.Controls.Add(pauseButton);
			ModernButton modernButton = new ModernButton();
			modernButton.Text = "Apps";
			modernButton.Location = new Point(145, 299);
			modernButton.Size = new Size(107, 48);
			modernButton.FillColor = Theme.AccentSoft;
			modernButton.HoverColor = Color.FromArgb(226, 229, 255);
			modernButton.TextColor = Theme.Accent;
			ModernButton modernButton2 = modernButton;
			modernButton2.Click += delegate
			{
				Flush();
				using AppUsageForm appUsageForm = new AppUsageForm(repository, settings);
				appUsageForm.ShowDialog(this);
			};
			cardPanel2.Controls.Add(modernButton2);
			ModernButton modernButton3 = new ModernButton();
			modernButton3.Text = "History";
			modernButton3.Location = new Point(262, 299);
			modernButton3.Size = new Size(107, 48);
			modernButton3.FillColor = Theme.AccentSoft;
			modernButton3.HoverColor = Color.FromArgb(226, 229, 255);
			modernButton3.TextColor = Theme.Accent;
			ModernButton modernButton4 = modernButton3;
			modernButton4.Click += delegate
			{
				Flush();
				using HistoryForm historyForm = new HistoryForm(repository);
				historyForm.ShowDialog(this);
			};
			cardPanel2.Controls.Add(modernButton4);
			ModernButton modernButton5 = new ModernButton();
			modernButton5.Text = "Records";
			modernButton5.Location = new Point(379, 299);
			modernButton5.Size = new Size(114, 48);
			modernButton5.FillColor = Color.FromArgb(244, 245, 249);
			modernButton5.HoverColor = Theme.AccentSoft;
			modernButton5.TextColor = Theme.Text;
			ModernButton modernButton6 = modernButton5;
			modernButton6.Click += delegate
			{
				Process.Start(root);
			};
			cardPanel2.Controls.Add(modernButton6);
			CardPanel cardPanel3 = new CardPanel();
			cardPanel3.Location = new Point(577, 101);
			cardPanel3.Size = new Size(291, 174);
			CardPanel cardPanel4 = cardPanel3;
			base.Controls.Add(cardPanel4);
			cardPanel4.Controls.Add(Ui.Label("CURRENT ACTIVITY", 24, 22, 200, 24, 8f, FontStyle.Bold, Theme.Muted));
			appLabel = Ui.Label("Detecting…", 24, 53, 235, 35, 14f, FontStyle.Bold, Theme.Text);
			appLabel.AutoEllipsis = true;
			cardPanel4.Controls.Add(appLabel);
			idleLabel = Ui.Label("", 24, 99, 235, 42, 9f, FontStyle.Regular, Theme.Muted);
			cardPanel4.Controls.Add(idleLabel);
			CardPanel cardPanel5 = new CardPanel();
			cardPanel5.Location = new Point(577, 296);
			cardPanel5.Size = new Size(291, 187);
			CardPanel cardPanel6 = cardPanel5;
			base.Controls.Add(cardPanel6);
			cardPanel6.Controls.Add(Ui.Label("TRACKING RULES", 24, 21, 180, 24, 8f, FontStyle.Bold, Theme.Muted));
			rulesLabel = Ui.Label("", 24, 52, 240, 50, 9f, FontStyle.Regular, Theme.Text);
			cardPanel6.Controls.Add(rulesLabel);
			ModernButton modernButton7 = new ModernButton();
			modernButton7.Text = "Edit rules";
			modernButton7.Location = new Point(24, 124);
			modernButton7.Size = new Size(110, 38);
			modernButton7.FillColor = Color.White;
			modernButton7.HoverColor = Theme.AccentSoft;
			modernButton7.TextColor = Theme.Accent;
			ModernButton modernButton8 = modernButton7;
			modernButton8.Click += EditSettings;
			cardPanel6.Controls.Add(modernButton8);
			ModernButton modernButton9 = new ModernButton();
			modernButton9.Text = "Transfer";
			modernButton9.Location = new Point(148, 124);
			modernButton9.Size = new Size(114, 38);
			modernButton9.FillColor = Theme.AccentSoft;
			modernButton9.HoverColor = Color.FromArgb(226, 229, 255);
			modernButton9.TextColor = Theme.Accent;
			ModernButton modernButton10 = modernButton9;
			modernButton10.Click += delegate
			{
				Flush();
				using DataTransferForm dataTransferForm = new DataTransferForm(repository);
				if (dataTransferForm.ShowDialog(this) == DialogResult.OK)
				{
					settings = AppSettings.Load(settingsPath);
					repository.SetWorkProcessNames(settings.workProcessNames);
					repository.LoadDay(currentDate, out work, out inactive);
					UpdateUi();
				}
			};
			cardPanel6.Controls.Add(modernButton10);
			syncLabel = Ui.Label("Reading synchronized records…", 34, 494, 825, 27, 8f, FontStyle.Regular, Theme.Muted);
			syncLabel.AutoEllipsis = true;
			base.Controls.Add(syncLabel);
			base.Controls.Add(Ui.Label("Deduplicated across devices · Statistics timezone: UTC+08:00", 34, 522, 620, 28, 8.5f, FontStyle.Regular, Theme.Muted));
			Label label = Ui.Label("Open data folder", 734, 522, 134, 28, 8.5f, FontStyle.Bold, Theme.Accent);
			label.Cursor = Cursors.Hand;
			label.Click += delegate
			{
				Process.Start(root);
			};
			base.Controls.Add(label);
		}

		private void Evaluate()
		{
			idle = NativeMethods.GetIdleSeconds();
			process = NativeMethods.GetForegroundProcessName();
			if (paused || suspended)
			{
				state = "Paused";
				reason = "Paused by user";
			}
			else if (!settings.workProcessNames.Any((string name) => string.Equals(name, process, StringComparison.OrdinalIgnoreCase)))
			{
				state = "Inactive";
				reason = "App is not in the work list";
			}
			else if (idle <= (double)settings.inputActiveSeconds)
			{
				state = "Work";
				reason = "Recent input in a work app";
			}
			else if (idle <= (double)settings.workAppIdleGraceSeconds)
			{
				state = "Work";
				reason = "Work app in foreground";
			}
			else
			{
				state = "Inactive";
				reason = "No focus condition matched";
			}
		}

		private void PowerChanged(object sender, PowerModeChangedEventArgs e)
		{
			if (base.IsDisposed || !base.IsHandleCreated)
			{
				return;
			}
			BeginInvoke((Action)delegate
			{
				if (e.Mode == PowerModes.Suspend)
				{
					SafeFlush();
					suspended = true;
				}
				else if (e.Mode == PowerModes.Resume)
				{
					suspended = false;
					lastTick = DateTime.Now;
					Evaluate();
				}
			});
		}

		private void SessionChanged(object sender, SessionSwitchEventArgs e)
		{
			if (base.IsDisposed || !base.IsHandleCreated)
			{
				return;
			}
			BeginInvoke((Action)delegate
			{
				if (e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.SessionLogoff)
				{
					SafeFlush();
					suspended = true;
				}
				else if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.SessionLogon)
				{
					suspended = false;
					lastTick = DateTime.Now;
					Evaluate();
				}
			});
		}

		private void Tick(object sender, EventArgs e)
		{
			try
			{
				DateTime now = DateTime.Now;
				double totalSeconds = (now - lastTick).TotalSeconds;
				lastTick = now;
				if (totalSeconds <= 0.0 || totalSeconds >= 5.0)
				{
					SaveSegment();
					Evaluate();
				}
				else if (!paused && !suspended)
				{
					string text = process;
					string text2 = state;
					string text3 = reason;
					double num = idle;
					Evaluate();
					string text4 = process;
					string text5 = state;
					string text6 = reason;
					double num2 = idle;
					if (text != text4 || text2 != text5)
					{
						process = text;
						state = text2;
						reason = text3;
						idle = num;
						SaveSegment();
						process = text4;
						state = text5;
						reason = text6;
						idle = num2;
					}
					segment += totalSeconds;
					segmentEnd = now;
					if (segment >= (double)settings.pollIntervalSeconds)
					{
						SaveSegment();
					}
				}
				string text7 = ActivityRepository.Today.ToString("yyyy-MM-dd");
				bool flag = !string.Equals(text7, currentDate, StringComparison.Ordinal) || now - lastSharedFlush >= ActivityRepository.SharedWriteInterval;
				if (flag)
				{
					SaveSegment();
					repository.FlushPendingToShared();
					lastSharedFlush = now;
					currentDate = text7;
					repository.RefreshAsync();
					lastRefreshRequest = now;
				}
				else
				{
					currentDate = text7;
				}
				if (!flag && (now - lastRefreshRequest).TotalSeconds >= 30.0)
				{
					repository.RefreshAsync();
					lastRefreshRequest = now;
				}
				repository.LoadDay(currentDate, out work, out inactive);
				UpdateUi();
			}
			catch (Exception ex)
			{
				paused = true;
				pauseButton.Text = "Resume";
				recordingError = "Recording paused: " + ex.Message;
				Evaluate();
				UpdateUi();
				LogError(ex);
			}
		}

		private void SaveSegment()
		{
			if (!(segment <= 0.0))
			{
				repository.AppendSegment(currentDate, segmentEnd, state, reason, idle, process, segment);
				segment = 0.0;
				lastSave = DateTime.Now;
			}
		}

		private void Flush(bool shared = false)
		{
			SaveSegment();
			if (shared)
			{
				repository.FlushPendingToShared();
				repository.Refresh(forceSummary: true);
				lastSharedFlush = DateTime.Now;
			}
			else
			{
				repository.RefreshAsync();
			}
			lastSave = DateTime.Now;
		}

		private void SafeFlush()
		{
			try
			{
				Flush(shared: true);
			}
			catch (Exception e)
			{
				paused = true;
				LogError(e);
			}
		}

		private static void LogError(Exception e)
		{
			try
			{
				string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusStarWindows");
				Directory.CreateDirectory(text);
				File.AppendAllText(Path.Combine(text, "errors.log"), DateTime.UtcNow.ToString("o") + " " + e?.ToString() + Environment.NewLine);
			}
			catch
			{
			}
		}

		private void UpdateUi()
		{
			string text = ActivityRepository.FormatDuration(work);
			timeLabel.Text = text;
			workLabel.Text = ActivityRepository.FormatDuration(repository.AllFocus);
			DayTotal day = repository.GetDay(ActivityRepository.Today);
			deviceLabel.Text = "Windows: " + ActivityRepository.FormatDuration(day.Windows) + "     Mac: " + ActivityRepository.FormatDuration(day.Mac);
			syncLabel.Text = ((recordingError != "") ? recordingError : (string.IsNullOrEmpty(repository.Warning) ? ("Synced at " + repository.LastRefresh.ToString("HH:mm:ss") + " · Overlapping focus counted once") : repository.Warning));
			syncLabel.ForeColor = (string.IsNullOrEmpty(repository.Warning) ? Theme.Muted : Theme.Amber);
			if (trayIcon != null)
			{
				trayIcon.Text = "Focus Star · " + text + (string.IsNullOrEmpty(repository.Warning) ? "" : " · Sync pending");
			}
			inactiveLabel.Text = ActivityRepository.FormatDuration(inactive);
			ring.WorkSeconds = work;
			ring.GoalSeconds = (double)settings.dailyGoalMinutes * 60.0;
			ring.Invalidate();
			appLabel.Text = (string.IsNullOrWhiteSpace(process) ? "No foreground app" : process);
			idleLabel.Text = ((idle < 60.0) ? string.Format(CultureInfo.InvariantCulture, "Last input: {0:0} seconds ago", idle) : string.Format(CultureInfo.InvariantCulture, "Last input: {0:0} minutes ago", idle / 60.0));
			rulesLabel.Text = string.Format(CultureInfo.InvariantCulture, "Input activity: {0} min\r\nWork app grace: {1} min", settings.inputActiveSeconds / 60, settings.workAppIdleGraceSeconds / 60);
			if (state == "Work")
			{
				status.SetStatus("FOCUSING", Theme.Green, Theme.GreenSoft);
			}
			else if (state == "Inactive")
			{
				status.SetStatus("INACTIVE", Theme.Amber, Theme.AmberSoft);
			}
			else
			{
				status.SetStatus("PAUSED", Theme.Muted, Color.FromArgb(237, 239, 243));
			}
		}

		private void TogglePause(object sender, EventArgs e)
		{
			Flush(shared: true);
			paused = !paused;
			if (!paused)
			{
				recordingError = "";
			}
			pauseButton.Text = (paused ? "Resume" : "Pause");
			pauseButton.FillColor = (paused ? Theme.Green : Theme.Accent);
			pauseButton.HoverColor = (paused ? Color.FromArgb(29, 145, 97) : Color.FromArgb(69, 77, 224));
			lastTick = DateTime.Now;
			Evaluate();
			UpdateUi();
		}

		private void EditSettings(object sender, EventArgs e)
		{
			Flush();
			using SettingsForm settingsForm = new SettingsForm(settingsPath, settings);
			if (settingsForm.ShowDialog(this) == DialogResult.OK)
			{
				settings = AppSettings.Load(settingsPath);
				repository.SetWorkProcessNames(settings.workProcessNames);
				repository.LoadDay(currentDate, out work, out inactive);
				Evaluate();
				repository.UpdateOutputs(currentDate, work, inactive, state, reason, process, idle);
				UpdateUi();
			}
		}

		private new void Closing(object sender, FormClosingEventArgs e)
		{
			if (!exitRequested && e.CloseReason == CloseReason.UserClosing)
			{
				e.Cancel = true;
				HideToTray();
				if (!trayHintShown)
				{
					trayHintShown = true;
					trayIcon.ShowBalloonTip(2500, "Focus Star is still running", "Double-click the star to open it. Right-click and choose Exit Focus Star to quit.", ToolTipIcon.Info);
				}
			}
			else
			{
				timer.Stop();
				SafeFlush();
				SystemEvents.PowerModeChanged -= PowerChanged;
				SystemEvents.SessionSwitch -= SessionChanged;
				trayIcon.Visible = false;
				trayIcon.Dispose();
				trayMenu.Dispose();
			}
		}
	}
	internal sealed class MutexGuard : IDisposable
	{
		private readonly Mutex mutex;

		public bool HasHandle { get; private set; }

		public MutexGuard(string name)
		{
			mutex = new Mutex(initiallyOwned: false, name);
			try
			{
				HasHandle = mutex.WaitOne(0, exitContext: false);
			}
			catch (AbandonedMutexException)
			{
				HasHandle = true;
			}
		}

		public void Dispose()
		{
			if (HasHandle)
			{
				mutex.ReleaseMutex();
			}
			mutex.Dispose();
		}
	}
	internal static class Program
	{
		[STAThread]
		private static void Main(string[] args)
		{
			NativeMethods.SetProcessDPIAware();
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(defaultValue: false);
			string rootDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
			bool flag = args.Contains("--preview");
			Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
			Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
			{
				try
				{
					string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusStarWindows");
					Directory.CreateDirectory(text);
					File.AppendAllText(Path.Combine(text, "errors.log"), DateTime.UtcNow.ToString("o") + " " + e.Exception?.ToString() + Environment.NewLine);
				}
				catch
				{
				}
				MessageBox.Show("Operation could not complete. Your history has not been deleted.\n" + e.Exception.Message, "Focus Star", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			};
			using MutexGuard mutexGuard = new MutexGuard(flag ? "Local\\FocusStarPreview" : "Local\\FocusStarRecorderGui");
			if (!mutexGuard.HasHandle)
			{
				MessageBox.Show("Focus Star is already running.", "Focus Star", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			bool launchInBackground = args.Any((string arg) => string.Equals(arg, "--background", StringComparison.OrdinalIgnoreCase));
			Application.Run(new MainForm(rootDirectory, launchInBackground, flag));
		}
	}
	internal sealed class FocusSpan
	{
		public double Start;

		public double End;

		public string Platform;

		public string App;

		public string State;
	}
	internal sealed class DayTotal
	{
		public double Focus;

		public double Windows;

		public double Mac;

		public double Inactive;

		public List<FocusSpan> Spans = new List<FocusSpan>();

		public List<FocusSpan> InactiveSpans = new List<FocusSpan>();
	}
	internal sealed class ActivityRepository
	{
		private readonly string root;

		private readonly string device;

		private readonly string output;

		private readonly string pendingDirectory;

		private readonly string settlementPath;

		private readonly SortedDictionary<DateTime, double> settledFocus = new SortedDictionary<DateTime, double>();

		private HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private volatile SortedDictionary<DateTime, DayTotal> snapshot = new SortedDictionary<DateTime, DayTotal>();

		private readonly Dictionary<string, List<FocusSpan>> cache = new Dictionary<string, List<FocusSpan>>(StringComparer.OrdinalIgnoreCase);

		private volatile bool busy;

		private readonly Dictionary<string, Task<string>> reads = new Dictionary<string, Task<string>>(StringComparer.OrdinalIgnoreCase);

		private readonly Dictionary<string, Task<string[]>> listings = new Dictionary<string, Task<string[]>>(StringComparer.OrdinalIgnoreCase);

		private string scanWarning = "";

		private readonly object refreshGate = new object();

		private readonly object pendingGate = new object();

		private DateTime lastSummaryWriteUtc = DateTime.MinValue;

		public static readonly TimeSpan SharedWriteInterval = TimeSpan.FromMinutes(10.0);

		private const string DetailedHeader = "timestamp,state,reason,idle_seconds,foreground_process,sample_seconds,platform,device_id,start_epoch,end_epoch";

		public static readonly TimeSpan StatisticsOffset = TimeSpan.FromHours(8.0);

		private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		public static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			{ "com.microsoft.VSCode", "Code" },
			{ "com.openai.codex", "Codex" },
			{ "com.openai.chat", "ChatGPT" },
			{ "notion.id", "Notion" },
			{ "com.kingsoft.wpsoffice.mac", "wps" },
			{ "com.kingsoft.wpsoffice.mac.global", "wps" },
			{ "com.microsoft.Word", "WINWORD" },
			{ "com.microsoft.Powerpoint", "POWERPNT" },
			{ "com.microsoft.Excel", "et" },
			{ "com.microsoft.Outlook", "OUTLOOK" },
			{ "com.github.GitHubClient", "GitHubDesktop" },
			{ "io.github.clash-verge-rev.clash-verge-rev", "clash-verge" }
		};

		public string Warning { get; private set; } = "Waiting for synchronized records";

		public DateTime LastRefresh { get; private set; }

		public string RootDirectory => root;

		public static DateTime Today => DateTimeOffset.UtcNow.ToOffset(StatisticsOffset).Date;

		public double AllFocus => snapshot.Values.Sum((DayTotal t) => t.Focus);

		public static string MapApp(string app)
		{
			string value;
			return Aliases.TryGetValue(app, out value) ? value : app;
		}

		public ActivityRepository(string rootDirectory)
			: this(rootDirectory, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusStarWindows"))
		{
		}

		internal ActivityRepository(string rootDirectory, string localStateDirectory)
		{
			root = rootDirectory;
			string text = localStateDirectory;
			Directory.CreateDirectory(text);
			string path = Path.Combine(text, "device-id");
			if (!File.Exists(path))
			{
				File.WriteAllText(path, Guid.NewGuid().ToString("D"));
			}
			device = File.ReadAllText(path).Trim();
			if (!Guid.TryParse(device, out var _))
			{
				throw new IOException("Invalid local Focus Star device ID");
			}
			output = Path.Combine(root, "windows-reports", device);
			pendingDirectory = Path.Combine(text, "pending", device);
			settlementPath = Path.Combine(text, "settled-daily.csv");
			Directory.CreateDirectory(pendingDirectory);
		}

		public void SetWorkProcessNames(string[] values)
		{
			names = new HashSet<string>(values ?? new string[0], StringComparer.OrdinalIgnoreCase);
		}

		public static List<string[]> ParseCsv(string text)
		{
			List<string[]> list = new List<string[]>();
			List<string> list2 = new List<string>();
			StringBuilder stringBuilder = new StringBuilder();
			bool flag = false;
			text = text.Replace("\ufeff", "").Replace("\r\n", "\n");
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				switch (c)
				{
				case '"':
					if (flag && i + 1 < text.Length && text[i + 1] == '"')
					{
						stringBuilder.Append(c);
						i++;
					}
					else
					{
						flag = !flag;
					}
					continue;
				case ',':
					if (!flag)
					{
						list2.Add(stringBuilder.ToString());
						stringBuilder.Clear();
						continue;
					}
					break;
				}
				if (c == '\n' && !flag)
				{
					list2.Add(stringBuilder.ToString());
					list.Add(list2.ToArray());
					list2.Clear();
					stringBuilder.Clear();
				}
				else if (c != '\r' || flag)
				{
					stringBuilder.Append(c);
				}
			}
			return list;
		}

		private static bool Number(string value, out double result)
		{
			return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && !double.IsNaN(result) && !double.IsInfinity(result);
		}

		internal static void MergeSavedFocusCsv(string text, IDictionary<DateTime, double> values)
		{
			List<string[]> rows = ParseCsv(text);
			if (rows.Count == 0)
			{
				return;
			}
			string[] header = rows[0];
			int dateColumn = Array.IndexOf(header, "date");
			int focusColumn = Array.IndexOf(header, "focus_seconds");
			int timezoneColumn = Array.IndexOf(header, "timezone");
			if (dateColumn < 0 || focusColumn < 0)
			{
				return;
			}
			foreach (string[] row in rows.Skip(1))
			{
				DateTime date;
				double seconds;
				if (row.Length != header.Length || !DateTime.TryParseExact(row[dateColumn], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ||
					!Number(row[focusColumn], out seconds) || seconds < 0.0 || seconds > 86400.0)
				{
					continue;
				}
				if (timezoneColumn >= 0 && !string.IsNullOrWhiteSpace(row[timezoneColumn]) && !row[timezoneColumn].Equals("UTC+08:00", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				double previous;
				if (!values.TryGetValue(date.Date, out previous) || seconds > previous)
				{
					values[date.Date] = seconds;
				}
			}
		}

		internal static void ApplySettledFocus(SortedDictionary<DateTime, DayTotal> totals, IDictionary<DateTime, double> saved)
		{
			foreach (KeyValuePair<DateTime, double> item in saved)
			{
				DayTotal total;
				if (!totals.TryGetValue(item.Key.Date, out total))
				{
					total = new DayTotal();
					totals[item.Key.Date] = total;
				}
				total.Focus = Math.Max(total.Focus, item.Value);
			}
		}

		public static List<FocusSpan> ParseRecords(string text, string platform)
		{
			List<string[]> list = ParseCsv(text);
			List<FocusSpan> list2 = new List<FocusSpan>();
			if (list.Count == 0)
			{
				return list2;
			}
			string[] array = list[0];
			int num = Array.IndexOf(array, "timestamp");
			int num2 = Array.IndexOf(array, "state");
			int num3 = Array.IndexOf(array, "foreground_process");
			int num4 = Array.IndexOf(array, "sample_seconds");
			int num5 = Array.IndexOf(array, "start_epoch");
			int num6 = Array.IndexOf(array, "end_epoch");
			if (num < 0 || num2 < 0 || num3 < 0 || num4 < 0)
			{
				return list2;
			}
			foreach (string[] item in list.Skip(1))
			{
				if (item.Length != array.Length || !Number(item[num4], out var result) || result <= 0.0 || result > 3600.0)
				{
					continue;
				}
				if (num6 < 0 || !Number(item[num6], out var result2))
				{
					if (!DateTime.TryParseExact(item[num], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result3))
					{
						continue;
					}
					result2 = (DateTime.SpecifyKind(result3 - StatisticsOffset, DateTimeKind.Utc) - Epoch).TotalSeconds;
				}
				if (num5 < 0 || !Number(item[num5], out var result4))
				{
					result4 = result2 - result;
				}
				if (!(result2 <= result4) && !(result2 - result4 > 3600.0) && !(Math.Abs(result2 - result4 - result) >= 0.1) && !(result4 < -62135596800.0) && !(result2 > 253402214400.0))
				{
					list2.Add(new FocusSpan
					{
						Start = result4,
						End = result2,
						Platform = platform,
						App = item[num3],
						State = item[num2]
					});
				}
			}
			return list2;
		}

		public static double Union(IEnumerable<FocusSpan> spans)
		{
			return Merge(spans).Sum((FocusSpan s) => s.End - s.Start);
		}

		public static List<FocusSpan> Merge(IEnumerable<FocusSpan> spans)
		{
			FocusSpan[] array = (from s in spans
				where s.End > s.Start
				orderby s.Start
				select s).ToArray();
			if (array.Length == 0)
			{
				return new List<FocusSpan>();
			}
			double start = array[0].Start;
			double num = array[0].End;
			FocusSpan template = array[0];
			List<FocusSpan> merged = new List<FocusSpan>();
			foreach (FocusSpan item in array.Skip(1))
			{
				if (item.Start <= num)
				{
					num = Math.Max(num, item.End);
					continue;
				}
				merged.Add(new FocusSpan { Start=start, End=num, Platform=template.Platform, App=template.App, State=template.State });
				start = item.Start;
				num = item.End;
				template = item;
			}
			merged.Add(new FocusSpan { Start=start, End=num, Platform=template.Platform, App=template.App, State=template.State });
			return merged;
		}

		public static SortedDictionary<DateTime, DayTotal> Summarize(IEnumerable<FocusSpan> spans, HashSet<string> allowed)
		{
			SortedDictionary<DateTime, DayTotal> sortedDictionary = new SortedDictionary<DateTime, DayTotal>();
			foreach (FocusSpan span in spans)
			{
				bool flag = span.State == "Work" && allowed.Contains(span.App);
				if (!flag && span.State != "Work" && span.State != "Inactive")
				{
					continue;
				}
				double num = span.Start;
				while (num < span.End)
				{
					DateTime date = Epoch.AddSeconds(num).Add(StatisticsOffset).Date;
					double totalSeconds = (DateTime.SpecifyKind(date.AddDays(1.0) - StatisticsOffset, DateTimeKind.Utc) - Epoch).TotalSeconds;
					double num2 = Math.Min(span.End, totalSeconds);
					if (!sortedDictionary.TryGetValue(date, out var value))
					{
						value = (sortedDictionary[date] = new DayTotal());
					}
					if (flag)
					{
						FocusSpan item = new FocusSpan
						{
							Start = num,
							End = num2,
							App = span.App,
							Platform = span.Platform,
							State = span.State
						};
						value.Spans.Add(item);
					}
					else if (span.Platform == "Windows")
					{
						value.InactiveSpans.Add(new FocusSpan { Start=num, End=num2, Platform="Windows", App=span.App, State=span.State });
					}
					num = num2;
				}
			}
			foreach (DayTotal value2 in sortedDictionary.Values)
			{
				value2.Focus = Union(value2.Spans);
				value2.Windows = Union(value2.Spans.Where((FocusSpan s) => s.Platform == "Windows"));
				value2.Mac = Union(value2.Spans.Where((FocusSpan s) => s.Platform == "macOS"));
				value2.Inactive = Union(value2.InactiveSpans);
			}
			return sortedDictionary;
		}

		private static bool ReadableCsv(string file)
		{
			if (!Path.GetExtension(file).Equals(".csv", StringComparison.OrdinalIgnoreCase)) return false;
			string name = Path.GetFileNameWithoutExtension(file);
			if (name.Equals("history",StringComparison.OrdinalIgnoreCase)) return true;
			DateTime result;
			return name.Length >= 10 && DateTime.TryParseExact(name.Substring(0,10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
		}

		private IEnumerable<string> Sources()
		{
			string[] array = new string[3] { "data", "mac-data", "windows-data" };
			foreach (string folder in array)
			{
				string path = Path.Combine(root, folder);
				if (!listings.TryGetValue(path, out var job))
				{
					Dictionary<string, Task<string[]>> dictionary = listings;
					string key = path;
					Task<string[]> value;
					job = (value = Task.Run(() => Directory.Exists(path) ? Directory.GetFiles(path, "*.csv", (!(folder == "data")) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly) : new string[0]));
					dictionary[key] = value;
				}
				string[] files = new string[0];
				try
				{
					if (job.Wait(1000))
					{
						files = job.Result;
						listings.Remove(path);
					}
					else
					{
						scanWarning = "Directory sync pending: " + folder;
					}
				}
				catch (Exception)
				{
					listings.Remove(path);
					scanWarning = "Directory unreadable: " + folder;
				}
				string[] array2 = files;
				foreach (string f in array2)
				{
					if (ReadableCsv(f))
					{
						yield return f;
					}
				}
				job = null;
			}
			string[] pending = new string[0];
			try
			{
				if (Directory.Exists(pendingDirectory))
				{
					pending = Directory.GetFiles(pendingDirectory, "*.csv", SearchOption.TopDirectoryOnly);
				}
			}
			catch (Exception)
			{
				scanWarning = "Local recovery cache unreadable";
			}
			foreach (string file in pending)
			{
				if (ReadableCsv(file))
				{
					yield return file;
				}
			}
		}

		private static string ReadShared(string path)
		{
			using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
			return streamReader.ReadToEnd();
		}

		private string ReadBounded(string path)
		{
			if (!reads.TryGetValue(path, out var value))
			{
				value = (reads[path] = Task.Run(() => ReadShared(path)));
			}
			try
			{
				if (!value.Wait(1000))
				{
					throw new IOException("Cloud read pending");
				}
				return value.Result;
			}
			catch (AggregateException ex)
			{
				throw new IOException("Cloud read failed", ex.InnerException);
			}
			finally
			{
				if (value.IsCompleted)
				{
					reads.Remove(path);
				}
			}
		}

		public void RefreshAsync()
		{
			if (busy)
			{
				return;
			}
			busy = true;
			Task.Run(delegate
			{
				try
				{
					Refresh();
				}
				catch (Exception ex)
				{
					Warning = "Sync read pending: " + ex.Message;
				}
				finally
				{
					busy = false;
				}
			});
		}

		public void Refresh()
		{
			Refresh(forceSummary: false);
		}

		public void Refresh(bool forceSummary)
		{
			lock (refreshGate)
			{
				RefreshCore(forceSummary);
			}
		}

		private void RefreshCore(bool forceSummary)
		{
			List<string> list = new List<string>();
			HashSet<string> allowed = names;
			scanWarning = "";
			ArchiveRunResult archived = CrossDayArchive.ArchivePastWindows(root,device,Today);
			foreach (string deleted in archived.DeletedFiles) cache.Remove(deleted);
			list.AddRange(archived.Warnings);
			string path = Path.Combine(root, "settings-mac.json");
			try
			{
				if (File.Exists(path))
				{
					if ((File.GetAttributes(path) & FileAttributes.Offline) != 0)
					{
						list.Add("settings-mac.json offline");
					}
					else
					{
						using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(ReadBounded(path)));
						AppSettings appSettings = (AppSettings)new DataContractJsonSerializer(typeof(AppSettings)).ReadObject((Stream)stream);
						if (appSettings.workProcessNames == null)
						{
							throw new IOException("Missing Mac whitelist");
						}
						allowed = new HashSet<string>(appSettings.workProcessNames, StringComparer.OrdinalIgnoreCase);
					}
				}
				else
				{
					list.Add("Mac rules unavailable; Windows rules used");
				}
			}
			catch (Exception)
			{
				list.Add("Mac rules unreadable; Windows rules used");
			}
			foreach (string item in Sources())
			{
				try
				{
					if ((File.GetAttributes(item) & FileAttributes.Offline) != 0)
					{
						list.Add(Path.GetFileName(item) + " offline");
						continue;
					}
					List<FocusSpan> list2 = ParseRecords(ReadBounded(item), item.StartsWith(Path.Combine(root, "mac-data") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? "macOS" : "Windows");
					if (list2.Count == 0 && cache.ContainsKey(item))
					{
						list.Add(Path.GetFileName(item) + " temporarily incomplete");
					}
					else
					{
						cache[item] = list2;
					}
				}
				catch (IOException)
				{
					list.Add(Path.GetFileName(item) + " syncing");
				}
				catch (UnauthorizedAccessException)
				{
					list.Add(Path.GetFileName(item) + " unavailable");
				}
			}
			string macDir = Path.Combine(root, "mac-data");
			if (scanWarning != "")
			{
				list.Add(scanWarning);
			}
			if (Directory.Exists(macDir) && !cache.Keys.Any((string f) => f.StartsWith(macDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
			{
				list.Add("Mac records not downloaded");
			}
			SortedDictionary<DateTime, DayTotal> totals = Summarize(cache.Values.SelectMany((List<FocusSpan> s) => s), allowed);
			SettleDailyFocus(totals);
			snapshot = totals;
			LastRefresh = DateTime.Now;
			Warning = ((list.Count == 0) ? "" : ("Incomplete sync: " + string.Join("; ", list.Distinct().Take(3))));
			try
			{
				WriteSummary(forceSummary);
			}
			catch (Exception ex4)
			{
				Warning = "Summary pending: " + ex4.Message;
			}
		}

		private void SettleDailyFocus(SortedDictionary<DateTime, DayTotal> totals)
		{
			try
			{
				if (File.Exists(settlementPath))
				{
					MergeSavedFocusCsv(ReadShared(settlementPath), settledFocus);
				}
			}
			catch (Exception)
			{
				// Keep the in-memory high-water marks if the local ledger is temporarily unavailable.
			}
			foreach (string folder in new string[2] { "windows-reports", "mac-reports" })
			{
				string directory = Path.Combine(root, folder);
				string[] reports;
				try
				{
					reports = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.csv", SearchOption.AllDirectories) : new string[0];
				}
				catch (Exception)
				{
					continue;
				}
				foreach (string report in reports)
				{
					try
					{
						if ((File.GetAttributes(report) & FileAttributes.Offline) == 0)
						{
							MergeSavedFocusCsv(ReadBounded(report), settledFocus);
						}
					}
					catch (Exception)
					{
						// An incomplete or syncing report is ignored; an older valid high-water mark remains.
					}
				}
			}
			foreach (KeyValuePair<DateTime, DayTotal> item in totals)
			{
				double previous;
				if (!settledFocus.TryGetValue(item.Key, out previous) || item.Value.Focus > previous)
				{
					settledFocus[item.Key] = item.Value.Focus;
				}
			}
			ApplySettledFocus(totals, settledFocus);
			try
			{
				SaveSettledFocus();
			}
			catch (Exception)
			{
				// Statistics remain usable; the next refresh will retry the local ledger.
			}
		}

		private void SaveSettledFocus()
		{
			List<string> lines = new List<string> { "date,focus_seconds" };
			foreach (KeyValuePair<DateTime, double> item in settledFocus)
			{
				lines.Add(item.Key.ToString("yyyy-MM-dd") + "," + item.Value.ToString("F3", CultureInfo.InvariantCulture));
			}
			string content = string.Join(Environment.NewLine, lines) + Environment.NewLine;
			if (File.Exists(settlementPath) && string.Equals(ReadShared(settlementPath), content, StringComparison.Ordinal))
			{
				return;
			}
			Directory.CreateDirectory(Path.GetDirectoryName(settlementPath));
			string temporary = settlementPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
			try
			{
				File.WriteAllText(temporary, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				if (File.Exists(settlementPath))
				{
					File.Replace(temporary, settlementPath, null);
				}
				else
				{
					File.Move(temporary, settlementPath);
				}
			}
			finally
			{
				if (File.Exists(temporary))
				{
					File.Delete(temporary);
				}
			}
		}

		public DayTotal GetDay(DateTime date)
		{
			DayTotal value;
			return snapshot.TryGetValue(date.Date, out value) ? value : new DayTotal();
		}

		public void LoadDay(string date, out double work, out double inactive)
		{
			DayTotal day = GetDay(DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture));
			work = day.Focus;
			inactive = day.Inactive;
		}

		public SortedDictionary<DateTime, double> LoadAllWorkByDay()
		{
			return new SortedDictionary<DateTime, double>(snapshot.ToDictionary((KeyValuePair<DateTime, DayTotal> x) => x.Key, (KeyValuePair<DateTime, DayTotal> x) => x.Value.Focus));
		}

		public List<AppUsageStat> LoadAppUsage(DateTime? startDate, string[] unused)
		{
			return (from s in snapshot.Where((KeyValuePair<DateTime, DayTotal> d) => !startDate.HasValue || d.Key >= startDate.Value.Date).SelectMany((KeyValuePair<DateTime, DayTotal> d) => d.Value.Spans)
				group s by s.Platform + " · " + s.App into g
				let merged = Merge(g)
				select new AppUsageStat
				{
					ProcessName = g.Key,
					Seconds = merged.Sum((FocusSpan s) => s.End - s.Start),
					Sessions = merged.Count,
					IsWorkApp = true
				} into s
				orderby s.Seconds descending
				select s).ToList();
		}

		private static string Csv(string s)
		{
			return "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
		}

		public void AppendSegment(string date, DateTime time, string state, string reason, double idle, string process, double seconds)
		{
			if (seconds <= 0.0 || seconds >= 3600.0 || state == "Paused" || state == "Suspended")
			{
				return;
			}
			DateTimeOffset dateTimeOffset = new DateTimeOffset(time).ToUniversalTime();
			List<string> list = new List<string>
			{
				dateTimeOffset.ToOffset(StatisticsOffset).ToString("yyyy-MM-dd HH:mm:ss"),
				state,
				reason,
				idle.ToString("F1", CultureInfo.InvariantCulture),
				process,
				seconds.ToString("F3", CultureInfo.InvariantCulture)
			};
			double totalSeconds = (dateTimeOffset.UtcDateTime - Epoch).TotalSeconds;
			list.AddRange(new string[4]
			{
				"Windows",
				device,
				(totalSeconds - seconds).ToString("F3", CultureInfo.InvariantCulture),
				totalSeconds.ToString("F3", CultureInfo.InvariantCulture)
			});
			string path = Path.Combine(pendingDirectory, dateTimeOffset.ToOffset(StatisticsOffset).ToString("yyyy-MM-dd") + ".csv");
			lock (pendingGate)
			{
				Directory.CreateDirectory(pendingDirectory);
				using FileStream fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
				fileStream.Seek(0L, SeekOrigin.End);
				using StreamWriter streamWriter = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1024, leaveOpen: true);
				if (fileStream.Length == 0)
				{
					streamWriter.WriteLine(DetailedHeader);
				}
				streamWriter.WriteLine(string.Join(",", list.Select(Csv)));
				streamWriter.Flush();
				fileStream.Flush(flushToDisk: true);
			}
		}

		public bool FlushPendingToShared()
		{
			lock (refreshGate)
			{
				lock (pendingGate)
				{
					bool changed = false;
					if (!Directory.Exists(pendingDirectory))
					{
						return false;
					}
					foreach (string pendingFile in Directory.GetFiles(pendingDirectory, "*.csv", SearchOption.TopDirectoryOnly))
					{
						List<string[]> records = ParseCsv(ReadShared(pendingFile));
						if (records.Count == 0 || !records[0].Contains("start_epoch"))
						{
							throw new IOException("Local recovery cache is incomplete: " + Path.GetFileName(pendingFile));
						}
						List<string[]> rows = records.Skip(1).Where((string[] row) => row.Length >= 10).ToList();
						if (rows.Count != records.Count - 1)
						{
							throw new IOException("Local recovery cache has an incomplete row: " + Path.GetFileName(pendingFile));
						}
						if (rows.Count == 0)
						{
							File.Delete(pendingFile);
							cache.Remove(pendingFile);
							continue;
						}
						// Each Windows installation owns its daily CSV; legacy data/*.csv remains read-only.
						string dataDirectory = Path.Combine(root, "windows-data", device, "v2");
						Directory.CreateDirectory(dataDirectory);
						string target = Path.Combine(dataDirectory, Path.GetFileName(pendingFile));
						using (FileStream fileStream = new FileStream(target, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
						{
							bool hasEpoch = true;
							if (fileStream.Length > 0)
							{
								using StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 1024, leaveOpen: true);
								hasEpoch = streamReader.ReadLine()?.Contains("start_epoch") ?? false;
							}
							fileStream.Seek(0L, SeekOrigin.End);
							using StreamWriter streamWriter = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1024, leaveOpen: true);
							if (fileStream.Length == 0)
							{
								streamWriter.WriteLine(DetailedHeader);
							}
							foreach (string[] row in rows)
							{
								streamWriter.WriteLine(string.Join(",", (hasEpoch ? row.Take(10) : row.Take(6)).Select(Csv)));
							}
							streamWriter.Flush();
							fileStream.Flush(flushToDisk: true);
						}
						File.Delete(pendingFile);
						cache.Remove(pendingFile);
						cache.Remove(target);
						changed = true;
					}
					return changed;
				}
			}
		}

		private void WriteSummary(bool force)
		{
			DateTime utcNow = DateTime.UtcNow;
			if (!force && utcNow - lastSummaryWriteUtc < SharedWriteInterval)
			{
				return;
			}
			Directory.CreateDirectory(output);
			List<string> list = new List<string> { "date,focus_seconds,windows_seconds,mac_seconds,timezone,sync_status" };
			foreach (KeyValuePair<DateTime, DayTotal> item in snapshot)
			{
				list.Add(string.Join(",", new string[6]
				{
					item.Key.ToString("yyyy-MM-dd"),
					item.Value.Focus.ToString("F3", CultureInfo.InvariantCulture),
					item.Value.Windows.ToString("F3", CultureInfo.InvariantCulture),
					item.Value.Mac.ToString("F3", CultureInfo.InvariantCulture),
					"UTC+08:00",
					Warning
				}.Select(Csv)));
			}
			string path = Path.Combine(output, "daily-summary.csv");
			string content = string.Join(Environment.NewLine, list) + Environment.NewLine;
			if (!File.Exists(path) || !string.Equals(ReadShared(path), content, StringComparison.Ordinal))
			{
				File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			}
			lastSummaryWriteUtc = utcNow;
		}

		public void UpdateOutputs(string date, double work, double inactive, string state, string reason, string process, double idle)
		{
			RefreshAsync();
		}

		public void ExportBackup(string destination)
		{
			lock (refreshGate)
			{
				ExportBackupCore(destination);
			}
		}

		private void ExportBackupCore(string destination)
		{
			string text = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
			try
			{
				using (ZipArchive zipArchive = ZipFile.Open(text, ZipArchiveMode.Create))
				{
					scanWarning = "";
					string[] array = Sources().Concat(new string[2] { "settings.json", "settings-mac.json" }.Select((string n) => Path.Combine(root, n)).Where(File.Exists)).ToArray();
					if (scanWarning != "")
					{
						throw new IOException(scanWarning + ". Backup cancelled to avoid missing history.");
					}
					if (Directory.Exists(Path.Combine(root, "mac-data")) && !array.Any((string f) => f.StartsWith(Path.Combine(root, "mac-data") + Path.DirectorySeparatorChar)))
					{
						throw new IOException("Mac data has not been downloaded. Download mac-data before exporting.");
					}
					string[] array2 = array;
					foreach (string text2 in array2)
					{
						if ((File.GetAttributes(text2) & FileAttributes.Offline) != 0)
						{
							throw new IOException("Download before backup: " + text2);
						}
						ZipArchiveEntry zipArchiveEntry = zipArchive.CreateEntry(text2.Substring(root.Length + 1).Replace('\\', '/'));
						using Stream destination2 = zipArchiveEntry.Open();
						using FileStream fileStream = new FileStream(text2, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
						fileStream.CopyTo(destination2);
					}
				}
				if (File.Exists(destination))
				{
					File.Replace(text, destination, null);
				}
				else
				{
					File.Move(text, destination);
				}
			}
			finally
			{
				if (File.Exists(text))
				{
					File.Delete(text);
				}
			}
		}

		public void ImportBackup(string source)
		{
			using (ZipArchive zipArchive = ZipFile.OpenRead(source))
			{
				List<Tuple<ZipArchiveEntry, string>> list = new List<Tuple<ZipArchiveEntry, string>>();
				foreach (ZipArchiveEntry entry in zipArchive.Entries)
				{
					string text = entry.FullName.Replace('\\', '/');
					if (text.EndsWith("/") || (!text.StartsWith("data/") && !text.StartsWith("mac-data/") && !text.StartsWith("windows-data/") && !(text == "settings.json") && !(text == "settings-mac.json")))
					{
						continue;
					}
					string fullPath = Path.GetFullPath(Path.Combine(root, text.Replace('/', Path.DirectorySeparatorChar)));
					if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
					{
						throw new IOException("Unsafe backup path");
					}
					if (File.Exists(fullPath))
					{
						using Stream stream = entry.Open();
						using FileStream fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
						if (entry.Length != fileStream.Length)
						{
							throw new IOException("Existing history differs; nothing imported: " + text);
						}
						int num;
						while ((num = stream.ReadByte()) != -1)
						{
							if (num != fileStream.ReadByte())
							{
								throw new IOException("Existing history differs; nothing imported: " + text);
							}
						}
					}
					else
					{
						list.Add(Tuple.Create(entry, fullPath));
					}
				}
				foreach (Tuple<ZipArchiveEntry, string> item in list)
				{
					Directory.CreateDirectory(Path.GetDirectoryName(item.Item2));
					using Stream stream2 = item.Item1.Open();
					using FileStream destination = new FileStream(item.Item2, FileMode.CreateNew);
					stream2.CopyTo(destination);
				}
			}
			RefreshAsync();
		}

		public static string FormatDuration(double seconds)
		{
			long num = (long)Math.Max(0.0, Math.Floor(seconds / 60.0));
			return string.Format(CultureInfo.InvariantCulture, "{0}h {1}m", num / 60, num % 60);
		}
	}
}
