using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayApi.Data;
using RaceDayApi.Dtos;
using RaceDayApi.Models;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDayApi.Controllers;

[ApiController]
[Route("api")]
public class EntrancesController : RaceDayControllerBase
{
    private readonly RaceDayContext _db;

    public EntrancesController(RaceDayContext db)
    {
        _db = db;
    }

    [HttpPost("category/{id:int}/evententrance")]
    [Authorize(Roles = Roles.Participant)]
    [SwaggerOperation(
        Summary = "Enter an event",
        Description = "Enrols the logged-in Participant in the chosen category. The link between the participant, the event and the category is saved. No body is needed. Returns 409 if the participant is already in this category or already has another event on the same date. Participant only.")]
    [ProducesResponseType(typeof(EntranceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enter(int id)
    {
        var participantId = CurrentUserId;

        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category == null) return Missing("Category not found.");

        var participant = await _db.Participants.FindAsync(participantId);
        if (participant == null) return Missing("Participant not found.");

        var alreadyIn = await _db.EventEntrances.AnyAsync(en => en.ParticipantID == participantId && en.CategoryID == id);
        if (alreadyIn) return Conflict(new { message = "You have already entered this category." });

        var eventId = category.EventID;
        var day = category.Event!.EventDate.Date;
        var clash = await _db.EventEntrances.AnyAsync(en =>
            en.ParticipantID == participantId
            && en.Category!.EventID != eventId
            && en.Category.Event!.EventDate.Date == day);
        if (clash) return Conflict(new { message = "You are already entered in another event on the same date." });

        var entry = new EventEntrance
        {
            ParticipantID = participantId,
            CategoryID = id,
            EventEntranceDescription = $"{participant.FirstName} entered the {category.CategoryName} at {category.Event.EventName}"
        };

        _db.EventEntrances.Add(entry);
        await _db.SaveChangesAsync();

        entry.Participant = participant;
        entry.Category = category;

        return StatusCode(StatusCodes.Status201Created, entry.ToResponse());
    }

    [HttpGet("participant/me/evententrance")]
    [Authorize(Roles = Roles.Participant)]
    [SwaggerOperation(
        Summary = "List my entries",
        Description = "Returns every event entry of the logged-in Participant with the event and category details. Participant only.")]
    [ProducesResponseType(typeof(IEnumerable<EntranceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var participantId = CurrentUserId;

        var entries = await _db.EventEntrances
            .Include(en => en.Participant)
            .Include(en => en.Category).ThenInclude(c => c!.Event)
            .Where(en => en.ParticipantID == participantId)
            .OrderBy(en => en.Category!.Event!.EventDate)
            .ToListAsync();

        return Ok(entries.Select(en => en.ToResponse()));
    }

    [HttpGet("event/{id:int}/evententrance")]
    [Authorize(Roles = Roles.Organiser)]
    [SwaggerOperation(
        Summary = "List all entries for my event",
        Description = "Returns every enrolment for an event. Only the Organiser who owns the event can do this.")]
    [ProducesResponseType(typeof(IEnumerable<EntranceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int id)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return Missing("Event not found.");
        if (ev.OrganiserID != CurrentUserId) return NotOwner("You can only view entries for your own events.");

        var entries = await _db.EventEntrances
            .Include(en => en.Participant)
            .Include(en => en.Category).ThenInclude(c => c!.Event)
            .Where(en => en.Category!.EventID == id)
            .OrderBy(en => en.EntryDate)
            .ToListAsync();

        return Ok(entries.Select(en => en.ToResponse()));
    }

    [HttpDelete("evententrance/{id:int}")]
    [Authorize]
    [SwaggerOperation(
        Summary = "Withdraw an entry",
        Description = "Removes an entry. A Participant can only remove their own entry and an Organiser can only remove entries on their own events.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Withdraw(int id)
    {
        var entry = await _db.EventEntrances
            .Include(en => en.Category).ThenInclude(c => c!.Event)
            .FirstOrDefaultAsync(en => en.EventEntranceID == id);
        if (entry == null) return Missing("Entry not found.");

        var allowed = User.IsInRole(Roles.Participant)
            ? entry.ParticipantID == CurrentUserId
            : entry.Category!.Event!.OrganiserID == CurrentUserId;
        if (!allowed) return NotOwner("You cannot withdraw this entry.");

        _db.EventEntrances.Remove(entry);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
