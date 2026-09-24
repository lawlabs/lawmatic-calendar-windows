using System.Globalization;
using Kalends;
using LawMatic_Calendar.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace LawMatic_Calendar;

public sealed partial class MainPage : Page
{
    private readonly List<CalendarEvent> _events = DemoEvents.Create(DateTime.Today);
    private readonly Stack<List<CalendarEvent>> _undo = new();
    private readonly bool[] _visible = [true, true, true, true];
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-US");
    private DateTime _date = DateTime.Today;
    private KalendsMode _mode = KalendsMode.Week;
    private bool _ready, _syncing, _dialogOpen, _sidebarHidden, _courtTimeline;

    public MainPage()
    {
        InitializeComponent();
        CourtTimeline.Data = CourtTimelineDemo.Load().ToTimelineData();
        CourtTimeline.SelectedItem = new(global::CourtTimeline.CourtTimelineItemKind.Event, "hearing-appeal");
        Scheduler.Culture = DisplayCulture;
        Scheduler.FirstDayOfWeek = DayOfWeek.Monday;
        Scheduler.CreateEvent = draft => new CalendarEvent { Start = draft.Start, End = draft.End, IsAllDay = draft.IsAllDay };
        _ready = true;
        foreach (var key in new[] { VirtualKey.N, VirtualKey.T, VirtualKey.F, VirtualKey.Z, VirtualKey.Number1, VirtualKey.Number2, VirtualKey.Number3, VirtualKey.Number4, VirtualKey.Number5, VirtualKey.Number6 })
        {
            var shortcut = new KeyboardAccelerator { Key = key, Modifiers = VirtualKeyModifiers.Control };
            shortcut.Invoked += (_, args) => args.Handled = RunShortcut(key);
            Root.KeyboardAccelerators.Add(shortcut);
        }
        Scheduler.EditRequested += (_, e) => EditEvent(AsCalendarEvent(e.Event));
        Scheduler.TimeChanged += (_, e) =>
        {
            if (e.Event is not CalendarEvent item) return;
            Snapshot();
            item.Start = e.Start;
            item.End = e.End;
            Update();
            StatusText.Text = $"«{item.Title}»: {e.Start:dd.MM HH:mm} — {e.End:dd.MM HH:mm}";
        };
        Scheduler.DuplicateRequested += (_, e) =>
        {
            var source = AsCalendarEvent(e.Event);
            var copy = source.Copy();
            copy.Id = Guid.NewGuid().ToString("D");
            copy.Title = source.Title + " — copy";
            EditEvent(copy);
        };
        Scheduler.NavigateRequested += (_, e) => { _date = e.Date; SetMode(e.Mode); };
        Scheduler.StatusChanged += (_, text) => { if (!_courtTimeline) StatusText.Text = text; };
        Loaded += (_, _) =>
        {
            DispatcherQueue.TryEnqueue(() => ApplyTheme(ActualTheme));
            BuildFilters();
            Update(true);
        };
        ActualThemeChanged += (_, _) => { if (_ready) BuildFilters(); };
        SizeChanged += (_, e) =>
        {
            bool hide = _sidebarHidden || e.NewSize.Width < 1100;
            SidebarColumn.Width = new GridLength(hide ? 0 : 260);
            Sidebar.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
            SearchBox.Width = e.NewSize.Width < 900 ? 145 : 210;
        };
        var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
        ZoneText.Text = $"UTC{(offset < TimeSpan.Zero ? "−" : "+")}{offset.Duration():hh\\:mm}";
    }

    private static CalendarEvent AsCalendarEvent(IKalendsEvent item) =>
        item as CalendarEvent ?? new CalendarEvent
        {
            Id = item.Id,
            Title = item.Title,
            Start = item.Start,
            End = item.End,
            IsAllDay = item.IsAllDay,
            IsReadOnly = item.IsReadOnly,
            Location = item.Subtitle ?? "",
            Color = item.Color
        };

    private void BuildFilters()
    {
        CalendarFilters.Children.Clear();
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            var color = CalendarEvent.Palette[i];
            panel.Children.Add(new Border
            {
                Width = 9,
                Height = 9,
                CornerRadius = new CornerRadius(3),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, color.R, color.G, color.B))
            });
            panel.Children.Add(new TextBlock { Text = DemoEvents.CalendarNames[i], FontSize = 13 });
            var check = new CheckBox { Content = panel, IsChecked = _visible[i], HorizontalAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(check, DemoEvents.CalendarNames[i]);
            check.Checked += (_, _) => { _visible[index] = true; Update(); };
            check.Unchecked += (_, _) => { _visible[index] = false; Update(); };
            CalendarFilters.Children.Add(check);
        }
    }

    private void Update(bool resetScroll = false)
    {
        if (!_ready) return;
        Scheduler.Visibility = _courtTimeline ? Visibility.Collapsed : Visibility.Visible;
        CourtTimeline.Visibility = _courtTimeline ? Visibility.Visible : Visibility.Collapsed;
        TimelineSidebar.Visibility = _courtTimeline ? Visibility.Visible : Visibility.Collapsed;
        foreach (var element in new FrameworkElement[] { NewEventButton, MiniCalendarHost, CalendarFiltersSection, DateNavigation, CalendarOptions, SearchBox, UndoButton })
            element.Visibility = _courtTimeline ? Visibility.Collapsed : Visibility.Visible;
        if (_courtTimeline)
        {
            PeriodTitle.Text = "Судебный таймлайн";
            PeriodSubtitle.Text = "Стадии, события и дальнейший ход дела";
            StatusText.Text = "Демоданные · 19 сентября 2026 · Нажмите на стадию или событие, чтобы увидеть детали";
            return;
        }
        var first = KalendsDate.StartOfWeek(_date, DayOfWeek.Monday);
        string Capital(string value) => DisplayCulture.TextInfo.ToTitleCase(value);
        PeriodTitle.Text = _mode switch
        {
            KalendsMode.Day => _date.ToString("d MMMM yyyy", DisplayCulture),
            KalendsMode.Year => _date.Year.ToString(DisplayCulture),
            _ => Capital(_date.ToString("MMMM yyyy", DisplayCulture))
        };
        PeriodSubtitle.Text = _mode switch
        {
            KalendsMode.Week => $"{first.ToString("d MMMM", DisplayCulture)} — {first.AddDays(WeekendToggle.IsOn ? 6 : 4).ToString("d MMMM", DisplayCulture)}  ·  Week {ISOWeek.GetWeekOfYear(_date)}",
            KalendsMode.Day => Capital(_date.ToString("dddd", DisplayCulture)),
            KalendsMode.Month => "Your month at a glance",
            KalendsMode.Year => "Plans and important dates for the year",
            _ => $"Next 30 days · from {_date.ToString("d MMMM", DisplayCulture)}"
        };
        string query = SearchBox.Text?.Trim() ?? "";
        Scheduler.IsSearching = query.Length > 0;
        if (Scheduler.IsSearching) PeriodSubtitle.Text = "Search results · all dates";
        Scheduler.Events = _events.Where(e => _visible[e.Calendar] && (query.Length == 0 || (e.Title + " " + e.Location + " " + e.Notes).Contains(query, StringComparison.CurrentCultureIgnoreCase))).ToArray();
        Scheduler.Date = _date;
        Scheduler.Mode = _mode;
        Scheduler.ShowWeekends = WeekendToggle.IsOn;
        Scheduler.HourHeight = ZoomSlider.Value;
        Scheduler.Refresh(resetScroll);
        _syncing = true;
        if (MiniCalendar.SelectedDates.Count != 1 || MiniCalendar.SelectedDates[0].Date != _date.Date)
        {
            MiniCalendar.SelectedDates.Clear();
            MiniCalendar.SelectedDates.Add(new DateTimeOffset(_date));
            MiniCalendar.SetDisplayDate(new DateTimeOffset(_date));
        }
        _syncing = false;
        UndoButton.IsEnabled = _undo.Count > 0;
        if (query.Length > 0) StatusText.Text = $"Events found: {Scheduler.Events.Count()} · Searching all demo events";
    }

    private void Snapshot() => _undo.Push(_events.Select(e => e.Copy()).ToList());

    private void SetMode(KalendsMode mode)
    {
        if (_courtTimeline) StatusText.Text = "Double-click an empty time slot to create an event";
        _courtTimeline = false;
        _mode = mode;
        _syncing = true;
        ViewSelector.SelectedItem = ViewSelector.Items.First(x => (string)x.Tag == mode.ToString());
        _syncing = false;
        Update(true);
    }

    private void ShowCourtTimeline()
    {
        _courtTimeline = true;
        _syncing = true;
        ViewSelector.SelectedItem = ViewSelector.Items.First(x => (string)x.Tag == "CourtTimeline");
        _syncing = false;
        Update();
    }

    private void Navigate(int direction)
    {
        _date = _mode switch
        {
            KalendsMode.Year => _date.AddYears(direction),
            KalendsMode.Month => _date.AddMonths(direction),
            KalendsMode.Day => _date.AddDays(direction),
            KalendsMode.Agenda => _date.AddDays(direction * 30),
            _ => _date.AddDays(direction * 7)
        };
        Update();
    }

    private void Previous_Click(object sender, RoutedEventArgs e) => Navigate(-1);
    private void Next_Click(object sender, RoutedEventArgs e) => Navigate(1);
    private void Today_Click(object sender, RoutedEventArgs e) { _date = DateTime.Today; Update(true); }
    private void View_Changed(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_ready && !_syncing && sender.SelectedItem?.Tag is string mode)
        {
            if (mode == "CourtTimeline") ShowCourtTimeline();
            else SetMode(Enum.Parse<KalendsMode>(mode));
        }
    }
    private void MiniCalendar_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
    {
        if (_ready && !_syncing && args.AddedDates.Count > 0) { _date = args.AddedDates[0].Date; Update(); }
    }
    private void Search_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) { if (_ready) Update(); }
    private void Options_Changed(object sender, RoutedEventArgs e) { if (_ready) Update(); }
    private void Zoom_Changed(object sender, RangeBaseValueChangedEventArgs e) { if (_ready) Update(true); }
    private void New_Click(object sender, RoutedEventArgs e) => EditEvent(new() { Start = _date.AddHours(9), End = _date.AddHours(10) });
    private void Theme_Click(object sender, RoutedEventArgs e) =>
        ApplyTheme(ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark);

    private void ApplyTheme(ElementTheme theme)
    {
        if (theme == ElementTheme.Default) theme = ElementTheme.Dark;
        RequestedTheme = theme;
        MiniCalendar.RequestedTheme = theme;
        Scheduler.RequestedTheme = theme;
        if (App.MainAppWindow?.Content is FrameworkElement root) root.RequestedTheme = theme;
        if (App.MainAppWindow is { } window)
            window.AppWindow.TitleBar.ButtonForegroundColor = theme == ElementTheme.Dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
    }
    private void SidebarToggle_Click(object sender, RoutedEventArgs e)
    {
        _sidebarHidden = Sidebar.Visibility == Visibility.Visible;
        Sidebar.Visibility = _sidebarHidden ? Visibility.Collapsed : Visibility.Visible;
        SidebarColumn.Width = new GridLength(_sidebarHidden ? 0 : 260);
    }
    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_undo.Count == 0) return;
        _events.Clear();
        _events.AddRange(_undo.Pop());
        Update();
        StatusText.Text = "Change undone";
    }

    private bool RunShortcut(VirtualKey key)
    {
        if (_dialogOpen) return false;
        if (_courtTimeline && key is VirtualKey.N or VirtualKey.T or VirtualKey.F or VirtualKey.Z) return false;
        switch (key)
        {
            case VirtualKey.N: New_Click(this, new()); break;
            case VirtualKey.T: Today_Click(this, new()); break;
            case VirtualKey.F: SearchBox.Focus(FocusState.Keyboard); break;
            case VirtualKey.Z:
                if (FocusManager.GetFocusedElement(XamlRoot) is TextBox { CanUndo: true }) return false;
                Undo_Click(this, new()); break;
            case VirtualKey.Number1: SetMode(KalendsMode.Agenda); break;
            case VirtualKey.Number2: SetMode(KalendsMode.Day); break;
            case VirtualKey.Number3: SetMode(KalendsMode.Week); break;
            case VirtualKey.Number4: SetMode(KalendsMode.Month); break;
            case VirtualKey.Number5: SetMode(KalendsMode.Year); break;
            case VirtualKey.Number6: ShowCourtTimeline(); break;
            default: return false;
        }
        return true;
    }

    private async void Help_Click(object sender, RoutedEventArgs e)
    {
        if (_dialogOpen) return;
        _dialogOpen = true;
        try
        {
            await new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Using your calendar",
                CloseButtonText = "Got it",
                Content = new TextBlock
                {
                    Text = "Double-click the grid to create an event.\nClick an event to view or edit it.\nDrag an event to change its date and time.\nDrag its top or bottom edge to adjust its duration in 15-minute steps.\nIn Month view, drag events between days.\nEsc cancels a drag. Ctrl+Z undoes a change.\n\nCtrl+N — new event\nCtrl+T — today\nCtrl+F — search\nCtrl+1…5 — switch calendar views\nCtrl+6 — court timeline demo\nAlt+arrow keys — move the focused event\nAlt+Shift+↑/↓ — adjust duration\n\nThe grid is drawn by Kalends.WinUI. Changes are kept in memory until the app closes.",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14
                }
            }.ShowAsync();
        }
        finally { _dialogOpen = false; }
    }

    private async void EditEvent(CalendarEvent item)
    {
        if (_dialogOpen) return;
        _dialogOpen = true;
        try
        {
            bool exists = _events.Any(e => e.Id == item.Id);
            var title = new TextBox { Header = "Title", Text = item.Title == "New event" && !exists ? "" : item.Title, PlaceholderText = "Add a title", MaxLength = 200 };
            var startDate = new CalendarDatePicker { Header = "Start", Date = item.Start, DateFormat = "{day.integer} {month.full} {year.full}", HorizontalAlignment = HorizontalAlignment.Stretch };
            var endDate = new CalendarDatePicker { Header = "End", Date = item.IsAllDay ? item.End.AddDays(-1) : item.End, DateFormat = "{day.integer} {month.full} {year.full}", HorizontalAlignment = HorizontalAlignment.Stretch };
            var startTime = new TimePicker { Header = "Time", Time = item.Start.TimeOfDay, ClockIdentifier = "24HourClock", MinuteIncrement = 15 };
            var endTime = new TimePicker { Header = "Time", Time = item.End.TimeOfDay, ClockIdentifier = "24HourClock", MinuteIncrement = 15 };
            var allDay = new CheckBox { Content = "All day", IsChecked = item.IsAllDay };
            var calendar = new ComboBox { Header = "Calendar", ItemsSource = DemoEvents.CalendarNames, SelectedIndex = item.Calendar, HorizontalAlignment = HorizontalAlignment.Stretch };
            var location = new TextBox { Header = "Location", Text = item.Location, PlaceholderText = "Address or meeting link", MaxLength = 500 };
            var notes = new TextBox { Header = "Notes", Text = item.Notes, PlaceholderText = "Anything to remember", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 76, MaxLength = 5000 };
            var error = new InfoBar { IsOpen = false, Severity = InfoBarSeverity.Error, IsClosable = false };
            var form = new StackPanel { Spacing = 14, MinWidth = 440 };
            form.Children.Add(title);
            form.Children.Add(allDay);
            foreach (var pair in new[] { (startDate, startTime), (endDate, endTime) })
            {
                var row = new Grid { ColumnSpacing = 12 };
                row.ColumnDefinitions.Add(new());
                row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
                row.Children.Add(pair.Item1);
                Grid.SetColumn(pair.Item2, 1);
                row.Children.Add(pair.Item2);
                form.Children.Add(row);
            }
            void ToggleTimes() { startTime.IsEnabled = endTime.IsEnabled = allDay.IsChecked != true; }
            allDay.Checked += (_, _) => ToggleTimes();
            allDay.Unchecked += (_, _) => ToggleTimes();
            ToggleTimes();
            form.Children.Add(calendar);
            form.Children.Add(location);
            form.Children.Add(notes);
            form.Children.Add(error);
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                RequestedTheme = ActualTheme,
                Title = exists ? "Event" : "New event",
                Content = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 610 },
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                SecondaryButtonText = exists ? "Delete…" : "",
                DefaultButton = ContentDialogButton.Primary
            };
            DateTime newStart = item.Start, newEnd = item.End;
            dialog.PrimaryButtonClick += (_, args) =>
            {
                string? message = null;
                if (string.IsNullOrWhiteSpace(title.Text)) message = "Enter an event title.";
                else if (startDate.Date == null || endDate.Date == null) message = "Select both dates.";
                else
                {
                    newStart = startDate.Date.Value.Date + (allDay.IsChecked == true ? TimeSpan.Zero : startTime.Time);
                    newEnd = endDate.Date.Value.Date + (allDay.IsChecked == true ? TimeSpan.FromDays(1) : endTime.Time);
                    if (newEnd <= newStart) message = "End must be after start.";
                }
                if (message != null) { error.Message = message; error.IsOpen = true; args.Cancel = true; }
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                Snapshot();
                item.Title = title.Text.Trim();
                item.Start = newStart;
                item.End = newEnd;
                item.IsAllDay = allDay.IsChecked == true;
                item.Calendar = calendar.SelectedIndex;
                item.Location = location.Text.Trim();
                item.Notes = notes.Text.Trim();
                if (!exists) _events.Add(item);
                Update();
                StatusText.Text = $"Saved: {item.Title}";
            }
            else if (result == ContentDialogResult.Secondary)
            {
                var confirm = new ContentDialog { XamlRoot = XamlRoot, RequestedTheme = ActualTheme, Title = "Delete event?", Content = $"«{item.Title}»\nYou can undo this action with Ctrl+Z.", PrimaryButtonText = "Delete", CloseButtonText = "Cancel" };
                if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                {
                    Snapshot();
                    _events.RemoveAll(e => e.Id == item.Id);
                    Update();
                    StatusText.Text = "Event deleted · Ctrl+Z to restore";
                }
            }
        }
        catch (Exception exception)
        {
            StatusText.Text = "Could not open the editor: " + exception.Message;
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally { _dialogOpen = false; }
    }
}
