using System.Text.Json;
using System.Text.Json.Serialization;

namespace LawMatic_Calendar.Models;

// One fixture is shared with docs/court-timeline-preview.html. Demo dates are fixed
// deliberately: the preview remains reproducible after the real date changes.
public sealed class CourtTimelineDemo
{
    public DateOnly Today { get; set; }
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; } // Exclusive axis boundary.
    public string Number { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Amount { get; set; } = "";
    public List<CourtStageDemo> Stages { get; set; } = [];
    public List<CourtEventDemo> Events { get; set; } = [];

    public CourtTimeline.CourtTimelineData ToTimelineData() => new()
    {
        Start = Start, End = End, Today = Today, Number = Number, Title = Title,
        Subject = Subject, Amount = Amount, Status = "Апелляция · в производстве",
        Stages = Stages.Select(stage =>
        {
            var state = Enum.Parse<CourtTimeline.CourtStageState>(stage.State, ignoreCase: true);
            return new CourtTimeline.CourtStage
            {
                Id = stage.Id, Title = stage.Title, Court = stage.Court, Status = stage.Status,
                State = state,
                Tone = Enum.Parse<CourtTimeline.CourtStageTone>(stage.Tone, ignoreCase: true),
                Start = stage.Start,
                Closed = state == CourtTimeline.CourtStageState.Completed ? stage.End : null,
                Deadline = state == CourtTimeline.CourtStageState.Completed ? null : stage.End,
                Note = stage.Note
            };
        }).ToArray(),
        Events = Events.Select(item => new CourtTimeline.CourtEvent
        {
            Id = item.Id, StageId = item.Stage, Date = item.Date, Time = item.Time,
            Title = item.Title, Kind = item.Kind, Location = item.Location,
            Note = item.Note, Planned = item.Planned
        }).ToArray()
    };

    public static CourtTimelineDemo Load()
    {
        using var stream = typeof(CourtTimelineDemo).Assembly.GetManifestResourceStream(
            "LawMatic_Calendar.CourtTimelineDemo.json")
            ?? throw new InvalidOperationException("Court timeline demo data is missing.");
        return JsonSerializer.Deserialize(stream, CourtTimelineJsonContext.Default.CourtTimelineDemo)
            ?? throw new InvalidOperationException("Court timeline demo data is empty.");
    }
}

public sealed class CourtStageDemo
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Court { get; set; } = "";
    public string Status { get; set; } = "";
    public string State { get; set; } = "";
    public string Tone { get; set; } = "";
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; } // Last occupied calendar day, inclusive.
    public string Note { get; set; } = "";
}

public sealed class CourtEventDemo
{
    public string Id { get; set; } = "";
    public string Stage { get; set; } = "";
    public DateOnly Date { get; set; }
    public string Time { get; set; } = "";
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Location { get; set; } = "";
    public string Note { get; set; } = "";
    public bool Planned { get; set; }
}

// Release builds enable trimming: keep JSON metadata without reflection.
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CourtTimelineDemo))]
internal partial class CourtTimelineJsonContext : JsonSerializerContext
{
}
