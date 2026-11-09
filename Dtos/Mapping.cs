using RaceDayApi.Models;

namespace RaceDayApi.Dtos;

public static class Mapping
{
    public static EventResponse ToResponse(this Event e) =>
        new(e.EventID, e.EventName, e.EventDescription, e.EventDate, e.Location, e.DistanceKm, e.EventType.ToString(), e.OrganiserID);

    public static CategoryResponse ToResponse(this Category c) =>
        new(c.CategoryID, c.CategoryName, c.CategoryDescription, c.EventID);

    public static EntranceResponse ToResponse(this EventEntrance en) =>
        new(en.EventEntranceID,
            en.ParticipantID,
            $"{en.Participant!.FirstName} {en.Participant.LastName}",
            en.CategoryID,
            en.Category!.CategoryName,
            en.Category.EventID,
            en.Category.Event!.EventName,
            en.Category.Event.EventDate,
            en.EntryDate);

    public static ResultResponse ToResponse(this Result r) =>
        new(r.ResultID,
            r.EventEntranceID,
            $"{r.EventEntrance!.Participant!.FirstName} {r.EventEntrance.Participant.LastName}",
            r.EventEntrance.CategoryID,
            r.EventEntrance.Category!.CategoryName,
            r.EventEntrance.Category.Event!.EventName,
            r.FinishTime,
            r.FinishPosition,
            r.ResultDescription);
}
