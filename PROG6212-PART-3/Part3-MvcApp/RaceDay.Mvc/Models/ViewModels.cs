using System.ComponentModel.DataAnnotations;
using RaceDay.Mvc.Helpers;

namespace RaceDay.Mvc.Models;

public record ErrorViewModel(string Title, string Message);

// ---------- Account ----------

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please choose a role.")]
    [RegularExpression("^(Organiser|Participant)$", ErrorMessage = "Please choose Organiser or Participant.")]
    public string Role { get; set; } = "Participant";

    [Required(ErrorMessage = "Please enter your first name.")]
    [StringLength(50, ErrorMessage = "First name can be at most 50 characters.")]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your last name.")]
    [StringLength(50, ErrorMessage = "Last name can be at most 50 characters.")]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter a password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Your password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [StringLength(20)]
    [Display(Name = "Contact number")]
    public string? ContactNumber { get; set; }
}

// ---------- Public pages ----------

public class HomeIndexViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public string? Location { get; set; }
    public string? EventType { get; set; }
    public DateTime? Date { get; set; }
    public bool IncludePast { get; set; }
    public bool HasFilters => !string.IsNullOrWhiteSpace(Location) || !string.IsNullOrWhiteSpace(EventType) || Date.HasValue || IncludePast;
}

public class EventDetailsViewModel
{
    public EventDto Event { get; set; } = null!;
    public List<CategoryDto> Categories { get; set; } = new();
    public List<CategoryResults> Results { get; set; } = new();
    public HashSet<int> EnrolledCategoryIds { get; set; } = new();
    public bool IsOwner { get; set; }
}

/// <summary>One enrolment together with its result (if there is one), so the status can be worked out.</summary>
public class EntryRow
{
    public EntranceDto Entry { get; init; } = null!;
    public ResultDto? Result { get; init; }

    public string Status => Fmt.EntryStatus(Entry.EventDate, Result != null);
}

// ---------- Organiser ----------

public class OrganiserEventRow
{
    public EventDto Event { get; init; } = null!;
    public int CategoryCount { get; init; }
    public int EnrolmentCount { get; init; }
    public int ResultsCount { get; init; }
}

public class DashboardViewModel
{
    public List<OrganiserEventRow> Events { get; set; } = new();

    private static DateTime Today => DateTime.UtcNow.Date;

    public int TotalEvents => Events.Count;
    public int TotalEnrolments => Events.Sum(e => e.EnrolmentCount);
    public int UpcomingCount => Events.Count(e => e.Event.EventDate.Date >= Today);
    public int ResultsCaptured => Events.Sum(e => e.ResultsCount);
    public OrganiserEventRow? NextEvent => Events.Where(e => e.Event.EventDate.Date >= Today).OrderBy(e => e.Event.EventDate).FirstOrDefault();
}

public class EventFormViewModel
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Please enter the event name.")]
    [StringLength(100, ErrorMessage = "The name can be at most 100 characters.")]
    [Display(Name = "Event name")]
    public string EventName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "The description can be at most 500 characters.")]
    [Display(Name = "Description")]
    public string? EventDescription { get; set; }

    [Required(ErrorMessage = "Please choose the event date.")]
    [DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Please enter the location.")]
    [StringLength(150, ErrorMessage = "The location can be at most 150 characters.")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter the distance.")]
    [Range(0.1, 1000, ErrorMessage = "Distance must be between 0.1 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Required(ErrorMessage = "Please choose an event type.")]
    [RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "Please choose Run, Walk or Cycle.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    [Display(Name = "Banner image")]
    public IFormFile? BannerFile { get; set; }

    public bool RemoveBanner { get; set; }
}

public class CategoriesViewModel
{
    public EventDto Event { get; set; } = null!;
    public List<CategoryDto> Categories { get; set; } = new();
}

public class EventEntriesViewModel
{
    public EventDto Event { get; set; } = null!;
    public List<EntryRow> Entries { get; set; } = new();
}

public class ProfileViewModel
{
    [Required(ErrorMessage = "Please enter your first name.")]
    [StringLength(50)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your last name.")]
    [StringLength(50)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [StringLength(20)]
    [Display(Name = "Contact number")]
    public string? ContactNumber { get; set; }

    public string? Role { get; set; }
}

// ---------- Participant ----------

public class ParticipantResultRow
{
    public ResultDto Result { get; init; } = null!;
    public EntranceDto? Entry { get; init; }
    public int? Position { get; init; }
    public int? TotalFinishers { get; init; }
}
