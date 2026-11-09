using System.ComponentModel.DataAnnotations;

namespace RaceDayApi.Models;

public class EventEntrance
{
    public int EventEntranceID { get; set; }

    public int ParticipantID { get; set; }
    public Participant? Participant { get; set; }

    public int CategoryID { get; set; }
    public Category? Category { get; set; }

    [MaxLength(255)]
    public string? EventEntranceDescription { get; set; }

    public DateTime EntryDate { get; set; } = DateTime.UtcNow;

    public Result? Result { get; set; }
}
