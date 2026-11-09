using System.ComponentModel.DataAnnotations;

namespace RaceDayApi.Models;

public class Result
{
    public int ResultID { get; set; }

    [MaxLength(255)]
    public string? ResultDescription { get; set; }

    public TimeSpan? FinishTime { get; set; }

    public int? FinishPosition { get; set; }

    public int EventEntranceID { get; set; }
    public EventEntrance? EventEntrance { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
