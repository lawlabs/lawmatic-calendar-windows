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
    public List<CourtStage> Stages { get; set; } = [];
    public List<CourtEvent> Events { get; set; } = [];

    public static CourtTimelineDemo Load()
    {
        using var stream = typeof(CourtTimelineDemo).Assembly.GetManifestResourceStream(
            "LawMatic_Calendar.CourtTimelineDemo.json")
            ?? throw new InvalidOperationException("Court timeline demo data is missing.");
        return JsonSerializer.Deserialize(stream, CourtTimelineJsonContext.Default.CourtTimelineDemo)
            ?? throw new InvalidOperationException("Court timeline demo data is empty.");
    }
}

public sealed class CourtStage
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

public sealed class CourtEvent
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
