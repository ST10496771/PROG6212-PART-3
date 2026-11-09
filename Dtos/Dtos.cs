using System.ComponentModel.DataAnnotations;
using RaceDayApi.Models;

namespace RaceDayApi.Dtos;

public class RegisterRequest
{
    [Required]
    public string Role { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? ContactNumber { get; set; }
}

public class LoginRequest
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public record AccountResponse(int Id, string Role, string FirstName, string LastName, string Email, string? ContactNumber);

public class UpdateOrganiserRequest
{
    [Required, MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? ContactNumber { get; set; }
}

public class UpdateParticipantRequest
{
    [Required, MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string LastName { get; set; } = string.Empty;
}

public class EventRequest
{
    [Required, MaxLength(100)]
    public string EventName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? EventDescription { get; set; }

    [Required]
    public DateTime EventDate { get; set; }

    [Required, MaxLength(150)]
    public string Location { get; set; } = string.Empty;

    [Range(0.1, 1000)]
    public decimal DistanceKm { get; set; }

    [Required]
    public EventType EventType { get; set; }
}

public record EventResponse(int EventId, string EventName, string? EventDescription, DateTime EventDate, string Location, decimal DistanceKm, string EventType, int OrganiserId);

public class CategoryRequest
{
    [Required, MaxLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? CategoryDescription { get; set; }
}

public record CategoryResponse(int CategoryId, string CategoryName, string? CategoryDescription, int EventId);

public record EntranceResponse(int EventEntranceId, int ParticipantId, string ParticipantName, int CategoryId, string CategoryName, int EventId, string EventName, DateTime EventDate, DateTime EntryDate);

public class ResultRequest
{
    [Required]
    public TimeSpan FinishTime { get; set; }

    [Range(1, 100000)]
    public int? FinishPosition { get; set; }

    [MaxLength(255)]
    public string? ResultDescription { get; set; }
}

public record ResultResponse(int ResultId, int EventEntranceId, string ParticipantName, int CategoryId, string CategoryName, string EventName, TimeSpan? FinishTime, int? FinishPosition, string? ResultDescription);
