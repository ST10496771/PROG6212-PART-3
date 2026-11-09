using System.ComponentModel.DataAnnotations;

namespace RaceDayApi.Models;

public class Event
{
    public int EventID { get; set; }

    [Required, MaxLength(100)]
    public string EventName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? EventDescription { get; set; }

    public DateTime EventDate { get; set; }

    [Required, MaxLength(150)]
    public string Location { get; set; } = string.Empty;

    public decimal DistanceKm { get; set; }

    public EventType EventType { get; set; }

    public int OrganiserID { get; set; }
    public Organiser? Organiser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Category> Categories { get; set; } = new();
}
