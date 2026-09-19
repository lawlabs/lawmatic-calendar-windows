using System.Globalization;
using LawMatic_Calendar.Models;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace LawMatic_Calendar.Controls;

// Application-owned prototype. KalendsMode and the Kalends renderer stay unchanged.
public sealed partial class CourtTimelineView : UserControl
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ru-RU");
    private readonly CourtTimelineDemo _demo = CourtTimelineDemo.Load();
    private const double LabelWidth = 166;
    private const double RowHeight = 164;
    private double _zoom = 1;
    private string _selected = "hearing-appeal";
    private bool _ready;

    public CourtTimelineView()
    {
        InitializeComponent();
        CaseNumber.Text = "ДЕЛО № " + _demo.Number;
        CaseTitle.Text = _demo.Title;
        CaseSubject.Text = _demo.Subject;
        CaseAmount.Text = _demo.Amount;
        _ready = true;
        Loaded += (_, _) => Render();
        TimelineScroller.SizeChanged += (_, _) => Render();
        ActualThemeChanged += (_, _) => Render();
        SelectEvent(_demo.Events.Single(e => e.Id == _selected));
    }

    private SolidColorBrush Brush(string name)
    {
        var theme = new AccessibilitySettings().HighContrast ? "HighContrast"
            : ActualTheme == ElementTheme.Dark ? "Dark" : "Light";
        return (SolidColorBrush)((ResourceDictionary)Application.Current.Resources.ThemeDictionaries[theme])[name + "Brush"];
    }

    private SolidColorBrush Tone(string name, bool faint = false)
    {
        if (new AccessibilitySettings().HighContrast) return Brush(faint ? "Surface" : "Text");
        bool dark = ActualTheme == ElementTheme.Dark;
        uint rgb = name switch
        {
            "blue" => dark ? 0x7CB3F1u : 0x3979BBu,
            "purple" => dark ? 0xB5A1DEu : 0x77629Fu,
            _ => dark ? 0x80C8AEu : 0x347967u
        };
        return new SolidColorBrush(Color.FromArgb(faint ? (byte)32 : (byte)255,
            (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
    }

    private static string Day(DateOnly date) => date.ToString("d MMM", Culture).TrimEnd('.');
    private double X(DateOnly date) => LabelWidth + (date.DayNumber - _demo.Start.DayNumber)
        / (double)(_demo.End.DayNumber - _demo.Start.DayNumber) * (TimelineCanvas.Width - LabelWidth - 24);

    private void Put(FrameworkElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        TimelineCanvas.Children.Add(element);
    }

    private TextBlock Text(string value, double size, string brush = "Text", bool bold = false) => new()
    {
        Text = value, FontSize = size, Foreground = Brush(brush),
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        TextTrimming = TextTrimming.CharacterEllipsis
    };

    private void Line(double x1, double y1, double x2, double y2, SolidColorBrush brush, bool dashed = false)
    {
        var line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush, StrokeThickness = 1, IsHitTestVisible = false };
        if (dashed) line.StrokeDashArray = new DoubleCollection { 4, 4 };
        TimelineCanvas.Children.Add(line);
    }

    private void Render()
    {
        if (!_ready || TimelineScroller.ActualWidth <= 0) return;
        var focused = XamlRoot == null ? null : FocusManager.GetFocusedElement(XamlRoot) as Button;
        var focusId = focused?.Tag as string;
        var focusState = focused?.FocusState ?? FocusState.Unfocused;
        TimelineCanvas.Children.Clear();
        bool showPlan = PlanToggle.IsChecked == true;
        var stages = _demo.Stages.Where(s => showPlan || s.State != "potential").ToList();
        TimelineCanvas.Width = Math.Max(960, TimelineScroller.ActualWidth - 2) * _zoom;
        TimelineCanvas.Height = 60 + stages.Count * RowHeight;
        ZoomOutButton.IsEnabled = _zoom > 1;
        ZoomInButton.IsEnabled = _zoom < 4;
        FitButton.Content = _zoom == 1 ? "Весь процесс" : $"{_zoom:0.#}× · вписать";

        Put(new Border { Width = TimelineCanvas.Width, Height = 44, Background = Brush("Shell") }, 0, 0);
        Put(Text("ИНСТАНЦИЯ / 2026", 10, "Muted", true), 16, 15);
        for (var month = _demo.Start; month < _demo.End; month = month.AddMonths(1))
        {
            double x = X(month);
            double next = X(month.AddMonths(1));
            Line(x, 0, x, TimelineCanvas.Height, Brush("Line"));
            var label = Text(month.ToString("MMM", Culture).TrimEnd('.').ToUpper(Culture), 10, "Muted", true);
            label.Width = next - x;
            label.TextAlignment = TextAlignment.Center;
            Put(label, x, 15);
        }
        // At a closer scale expose individual calendar days, without timezone arithmetic.
        double dayWidth = X(_demo.Start.AddDays(1)) - X(_demo.Start);
        if (dayWidth >= 10)
        {
            for (var day = _demo.Start; day < _demo.End; day = day.AddDays(1))
            {
                Line(X(day), 44, X(day), TimelineCanvas.Height, Brush("SubtleLine"));
                if (dayWidth >= 20)
                {
                    var number = Text(day.Day.ToString(Culture), 9, "Muted");
                    number.Width = dayWidth;
                    number.TextAlignment = TextAlignment.Center;
                    Put(number, X(day), 46);
                }
            }
        }
        double todayX = X(_demo.Today);
        Line(todayX, 44, todayX, TimelineCanvas.Height, Brush("Now"), true);
        var todayLabel = new Border { Background = Brush("Surface"), Padding = new Thickness(5, 2, 5, 2), Child = Text("19 СЕН · СЕГОДНЯ (ДЕМО)", 9, "Now", true) };
        Put(todayLabel, Math.Clamp(todayX - 72, LabelWidth, TimelineCanvas.Width - 160), 45);

        for (int index = 0; index < stages.Count; index++)
        {
            var stage = stages[index];
            double y = 76 + index * RowHeight;
            var color = Tone(stage.Tone);
            Line(0, y + RowHeight - 16, TimelineCanvas.Width, y + RowHeight - 16, Brush("Line"));
            var title = Text(stage.Title, 12, bold: true); title.Width = LabelWidth - 28;
            Put(title, 16, y + 2);
            var court = Text(stage.Court, 10, "Muted"); court.Width = LabelWidth - 28;
            Put(court, 16, y + 25);
            var status = Text(stage.Status, 10, stage.State == "active" ? "Brand" : "Muted");
            status.Width = LabelWidth - 28;
            Put(status, 16, y + 46);

            DateOnly visualEnd = !showPlan && stage.State == "active" ? _demo.Today : stage.End;
            double left = X(stage.Start);
            double width = X(visualEnd.AddDays(1)) - left; // Closing day is included in full.
            var band = new Grid { Width = width, Height = 52 };
            double factWidth = stage.State == "potential" ? 0 : stage.State == "active"
                ? Math.Clamp(X(_demo.Today.AddDays(1)) - left, 0, width) : width;
            band.Children.Add(new Border { Width = factWidth, HorizontalAlignment = HorizontalAlignment.Left, Background = Tone(stage.Tone, true), CornerRadius = new CornerRadius(5) });
            var hatch = new Canvas { IsHitTestVisible = false, Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(factWidth, 0, width - factWidth, 52) } };
            for (double x = factWidth - 52; x < width; x += 9)
                hatch.Children.Add(new Line { X1 = x, Y1 = 52, X2 = x + 52, Y2 = 0, Stroke = color, Opacity = .12, StrokeThickness = 1 });
            band.Children.Add(hatch);
            var outline = new Rectangle { Stroke = color, StrokeThickness = 1, RadiusX = 5, RadiusY = 5, IsHitTestVisible = false };
            if (stage.State == "potential") outline.StrokeDashArray = new DoubleCollection { 5, 4 };
            band.Children.Add(outline);
            var copy = new StackPanel { Spacing = 3, Margin = new Thickness(12, 7, 10, 5) };
            var bandTitle = Text(stage.Title, 12, bold: true); bandTitle.Foreground = color;
            copy.Children.Add(bandTitle);
            copy.Children.Add(Text($"{Day(stage.Start)} — {Day(visualEnd)}" + (stage.State == "potential" ? " · возможно" : ""), 10, "Muted"));
            band.Children.Add(copy);
            var stageButton = new Button
            {
                Content = band, Tag = stage.Id, Padding = new Thickness(0), BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetName(stageButton, $"{stage.Title}, {stage.Status}, {Day(stage.Start)} — {Day(visualEnd)}");
            ToolTipService.SetToolTip(stageButton, stage.Note);
            stageButton.Click += (_, _) => SelectStage(stage);
            Put(stageButton, left, y);

            var laneEnds = new[] { LabelWidth - 10, LabelWidth - 10 };
            foreach (var item in _demo.Events.Where(e => e.Stage == stage.Id && (showPlan || !e.Planned)).OrderBy(e => e.Date))
            {
                double point = X(item.Date) + dayWidth / 2;
                double labelLeft = Math.Clamp(point - 12, LabelWidth, TimelineCanvas.Width - 150);
                int lane = laneEnds[0] + 8 <= labelLeft ? 0 : 1;
                laneEnds[lane] = labelLeft + 138;
                double labelTop = y + 69 + lane * 40;
                Line(point, y + 53, point, labelTop + 6, color, item.Planned);
                var marker = new Ellipse { Width = 8, Height = 8, Fill = item.Planned ? Brush("Surface") : color, Stroke = color, StrokeThickness = 1.5 };
                Put(marker, point - 4, y + 60);
                var eventCopy = new StackPanel { Spacing = 2 };
                eventCopy.Children.Add(Text(Day(item.Date) + (item.Time.Length > 0 ? " · " + item.Time : ""), 9, "Muted"));
                eventCopy.Children.Add(Text(item.Title, 10, bold: item.Id == _selected));
                var eventButton = new Button
                {
                    Content = eventCopy, Tag = item.Id, Width = 138, Padding = new Thickness(7, 3, 7, 3),
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = Brush(item.Id == _selected ? "Today" : "Surface"),
                    BorderBrush = item.Id == _selected ? color : Brush("Line"),
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4)
                };
                AutomationProperties.SetName(eventButton, $"{item.Title}, {Day(item.Date)} {item.Time}" + (item.Planned ? ", запланировано" : ""));
                ToolTipService.SetToolTip(eventButton, item.Note);
                eventButton.Click += (_, _) => { SelectEvent(item); Render(); };
                Canvas.SetZIndex(eventButton, 2);
                Put(eventButton, labelLeft, labelTop);
            }
            if (stage.State == "potential")
                Put(Text("После завершения апелляции · если решение будет обжаловано", 10, "Muted"), Math.Max(LabelWidth, left - 190), y + 78);
        }
        if (focusId != null && focusState != FocusState.Unfocused)
            TimelineCanvas.Children.OfType<Button>().FirstOrDefault(b => b.Tag as string == focusId)?.Focus(focusState);
    }

    private void SelectEvent(CourtEvent item)
    {
        _selected = item.Id;
        DetailKind.Text = item.Kind.ToUpper(Culture) + (item.Planned ? " · ЗАПЛАНИРОВАНО" : " · СОСТОЯЛОСЬ");
        DetailTitle.Text = item.Title;
        DetailDate.Text = item.Date.ToString("d MMMM yyyy", Culture) + (item.Time.Length > 0 ? " · " + item.Time : "");
        DetailLocation.Text = item.Location;
        DetailNote.Text = item.Note;
    }

    private void SelectStage(CourtStage stage)
    {
        _selected = stage.Id;
        DetailKind.Text = "СТАДИЯ · " + stage.Status.ToUpper(Culture);
        DetailTitle.Text = stage.Title;
        DetailDate.Text = $"{Day(stage.Start)} — {Day(stage.End)} 2026";
        DetailLocation.Text = stage.Court;
        DetailNote.Text = stage.Note;
        Render();
    }

    private void Plan_Click(object sender, RoutedEventArgs e)
    {
        if (PlanToggle.IsChecked != true && (_demo.Events.Any(x => x.Id == _selected && x.Planned) || _demo.Stages.Any(x => x.Id == _selected && x.State == "potential")))
            SelectStage(_demo.Stages.Single(x => x.State == "active"));
        Render();
    }
    private void ZoomIn_Click(object sender, RoutedEventArgs e) { _zoom = Math.Min(4, _zoom + .5); Render(); }
    private void ZoomOut_Click(object sender, RoutedEventArgs e) { _zoom = Math.Max(1, _zoom - .5); Render(); }
    private void Fit_Click(object sender, RoutedEventArgs e) { _zoom = 1; Render(); TimelineScroller.ChangeView(0, 0, null); }
}
