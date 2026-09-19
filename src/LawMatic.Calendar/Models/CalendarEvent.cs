using Kalends;

namespace LawMatic_Calendar.Models;

public sealed class CalendarEvent : IKalendsEvent
{
    public static readonly KalendsColor[] Palette =
    [
        KalendsColor.FromRgb(101, 81, 143),
        KalendsColor.FromRgb(56, 109, 174),
        KalendsColor.FromRgb(53, 118, 93),
        KalendsColor.FromRgb(151, 102, 30)
    ];

    public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public string Title { get; set; } = "New event";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool IsAllDay { get; set; }
    public bool IsReadOnly { get; set; }
    public string Location { get; set; } = "";
    public string Notes { get; set; } = "";
    public int Calendar { get; set; }
    public KalendsColor Color
    {
        get => Palette[Math.Clamp(Calendar, 0, Palette.Length - 1)];
        set
        {
            int index = Array.FindIndex(Palette, color => color == value);
            Calendar = index >= 0 ? index : 0;
        }
    }
    public string? Subtitle => string.IsNullOrWhiteSpace(Location) ? null : Location;

    public CalendarEvent Copy() => new()
    {
        Id = Id,
        Title = Title,
        Start = Start,
        End = End,
        IsAllDay = IsAllDay,
        IsReadOnly = IsReadOnly,
        Location = Location,
        Notes = Notes,
        Calendar = Calendar
    };
}

public static class DemoEvents
{
    public static readonly string[] CalendarNames = ["Work", "Meetings", "Personal", "Important dates"];

    public static List<CalendarEvent> Create(DateTime today)
    {
        var week = KalendsDate.StartOfWeek(today, DayOfWeek.Monday);
        var events = new List<CalendarEvent>();
        void Add(int day, double hour, double duration, string title, int color, string location = "") =>
            events.Add(new()
            {
                Title = title,
                Start = week.AddDays(day).AddHours(hour),
                End = week.AddDays(day).AddHours(hour + duration),
                Calendar = color,
                Location = location
            });
        Add(0, 9, 1, "Weekly planning", 0, "Office · meeting room");
        Add(0, 11, 1.5, "Project work", 0);
        Add(0, 14, 1, "Team meeting", 1, "Online");
        Add(0, 17, 1, "Workout", 2);
        Add(1, 9.5, 1.5, "Document preparation", 0);
        Add(1, 12, 1, "Lunch with Anna", 2, "North Café");
        Add(1, 14, 2, "Project discussion", 1, "Meeting room 2");
        Add(2, 9, 1, "Morning focus", 0);
        Add(2, 10.5, 1.5, "Consultation", 1, "Online");
        Add(2, 11, 1.5, "Partner call", 3);
        Add(2, 15, 1.5, "Research and notes", 0);
        Add(3, 9, 2, "Focus time", 0);
        Add(3, 12, 1, "Client meeting", 1, "Office · meeting room");
        Add(3, 15, 1, "Walk", 2);
        Add(4, 9.5, 1, "Results review", 0);
        Add(4, 11, 1.5, "Project presentation", 1, "Online");
        Add(4, 14, 1, "Weekly wrap-up", 3);
        Add(4, 17, 1.5, "Evening with friends", 2);
        Add(5, 10, 1.5, "Slow breakfast", 2);
        Add(5, 14, 2, "Exhibition", 2, "Museum of Modern Art");
        Add(6, 11, 1, "Time for yourself", 2);
        events.Add(new() { Title = "Project deadline", Start = week.AddDays(4), End = week.AddDays(5), IsAllDay = true, Calendar = 3 });
        events.Add(new() { Title = "Design days", Start = week.AddDays(1), End = week.AddDays(3), IsAllDay = true, Calendar = 1 });
        foreach (int offset in new[] { -14, -7, 7, 14 })
        {
            foreach (var item in events.Take(21).Where((_, i) => i % 3 == 0).ToArray())
                events.Add(WithOffset(item.Copy(), offset));
        }
        return events;
    }

    private static CalendarEvent WithOffset(CalendarEvent copy, int offset)
    {
        copy.Id = Guid.NewGuid().ToString("D");
        copy.Start = copy.Start.AddDays(offset);
        copy.End = copy.End.AddDays(offset);
        return copy;
    }
}
