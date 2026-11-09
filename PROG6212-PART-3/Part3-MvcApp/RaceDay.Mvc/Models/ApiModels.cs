namespace RaceDay.Mvc.Models;

// These mirror the JSON returned by the RaceDay API. The MVC app holds no database entities of its own.

public record AccountDto(int Id, string Role, string FirstName, string LastName, string Email, string? ContactNumber);

public record EventDto(
    int EventId, string EventName, string? EventDescription, DateTime EventDate,
    string Location, decimal DistanceKm, string EventType, int OrganiserId);

public record CategoryDto(int CategoryId, string CategoryName, string? CategoryDescription, int EventId);

public record EntranceDto(
    int EventEntranceId, int ParticipantId, string ParticipantName,
    int CategoryId, string CategoryName, int EventId, string EventName,
    DateTime EventDate, DateTime EntryDate);

public record ResultDto(
    int ResultId, int EventEntranceId, string ParticipantName, int CategoryId, string CategoryName,
    string EventName, TimeSpan? FinishTime, int? FinishPosition, string? ResultDescription);
