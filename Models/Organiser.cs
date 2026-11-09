using System.ComponentModel.DataAnnotations;

namespace RaceDayApi.Models;

public class Organiser
{
    public int OrganiserID { get; set; }

    [Required, MaxLength(50)]
    public string OrganiserFirstName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string OrganiserLastName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string OrganiserEmailAddress { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? OrganiserContactNumber { get; set; }

    [Required, MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Event> Events { get; set; } = new();
}
