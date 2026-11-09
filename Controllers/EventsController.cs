using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayApi.Data;
using RaceDayApi.Dtos;
using RaceDayApi.Models;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDayApi.Controllers;

[ApiController]
[Route("api/event")]
public class EventsController : RaceDayControllerBase
{
    private readonly RaceDayContext _db;

    public EventsController(RaceDayContext db)
    {
        _db = db;
    }

    [HttpGet]
    [SwaggerOperation(
        Summary = "List events",
        Description = "Returns all events ordered by date. Open to everyone. Optional query filters: date, location (partial match) and eventType (Run, Walk or Cycle).")]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] DateTime? date, [FromQuery] string? location, [FromQuery] EventType? eventType)
    {
        var query = _db.Events.AsQueryable();

        if (date.HasValue)
        {
            var day = date.Value.Date;
            query = query.Where(e => e.EventDate.Date == day);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            query = query.Where(e => e.Location.Contains(location));
        }

        if (eventType.HasValue)
        {
            query = query.Where(e => e.EventType == eventType.Value);
        }

        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(e => e.ToResponse()));
    }

    [HttpGet("{id:int}")]
    [SwaggerOperation(
        Summary = "Get one event",
        Description = "Returns the details of a single event by its id. Open to everyone.")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return Missing("Event not found.");

        return Ok(ev.ToResponse());
    }

    [HttpPost]
    [Authorize(Roles = Roles.Organiser)]
    [SwaggerOperation(
        Summary = "Create an event",
        Description = "Creates a new event owned by the logged-in Organiser. Expects eventName, eventDescription, eventDate, location, distanceKm and eventType (Run, Walk or Cycle). Organiser only.")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(EventRequest request)
    {
        var ev = new Event
        {
            EventName = request.EventName.Trim(),
            EventDescription = request.EventDescription,
            EventDate = request.EventDate,
            Location = request.Location.Trim(),
            DistanceKm = request.DistanceKm,
            EventType = request.EventType,
            OrganiserID = CurrentUserId
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = ev.EventID }, ev.ToResponse());
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [SwaggerOperation(
        Summary = "Update an event",
        Description = "Updates an event. Only the Organiser who created the event can do this. Same body as creating an event.")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, EventRequest request)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return Missing("Event not found.");
        if (ev.OrganiserID != CurrentUserId) return NotOwner("You can only change your own events.");

        ev.EventName = request.EventName.Trim();
        ev.EventDescription = request.EventDescription;
        ev.EventDate = request.EventDate;
        ev.Location = request.Location.Trim();
        ev.DistanceKm = request.DistanceKm;
        ev.EventType = request.EventType;
        await _db.SaveChangesAsync();

        return Ok(ev.ToResponse());
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [SwaggerOperation(
        Summary = "Delete an event",
        Description = "Deletes an event together with its categories, enrolments and results. Only the Organiser who created the event can do this.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return Missing("Event not found.");
        if (ev.OrganiserID != CurrentUserId) return NotOwner("You can only delete your own events.");

        var entries = await _db.EventEntrances.Where(en => en.Category!.EventID == id).ToListAsync();
        _db.EventEntrances.RemoveRange(entries);
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
