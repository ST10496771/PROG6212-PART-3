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
public class ResultsController : RaceDayControllerBase
{
    private readonly RaceDayContext _db;

    public ResultsController(RaceDayContext db)
    {
        _db = db;
    }

    [HttpPost("evententrance/{id:int}/result")]
    [Authorize(Roles = Roles.Organiser)]
    [SwaggerOperation(
        Summary = "Capture a result",
        Description = "Saves the finish time (hh:mm:ss) and optional finishing position for an entry. Only the Organiser who owns the event can do this. Returns 409 if a result was already captured for this entry.")]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Capture(int id, ResultRequest request)
    {
        if (request.FinishTime <= TimeSpan.Zero)
        {
            return BadRequest(new { message = "Finish time must be greater than zero." });
        }

        var entry = await _db.EventEntrances
            .Include(en => en.Participant)
            .Include(en => en.Category).ThenInclude(c => c!.Event)
            .FirstOrDefaultAsync(en => en.EventEntranceID == id);
        if (entry == null) return Missing("Entry not found.");
        if (entry.Category!.Event!.OrganiserID != CurrentUserId) return NotOwner("You can only capture results for your own events.");

        if (await _db.Results.AnyAsync(r => r.EventEntranceID == id))
        {
            return Conflict(new { message = "A result has already been captured for this entry." });
        }

        var result = new Result
        {
            EventEntranceID = id,
            FinishTime = request.FinishTime,
            FinishPosition = request.FinishPosition,
            ResultDescription = request.ResultDescription
                ?? $"{entry.Participant!.FirstName} finished the {entry.Category.CategoryName}"
        };

        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        result.EventEntrance = entry;

        return StatusCode(StatusCodes.Status201Created, result.ToResponse());
    }

    [HttpGet("event/{id:int}/result")]
    [SwaggerOperation(
        Summary = "Event results",
        Description = "Returns all results for an event, grouped by category and ordered by finish time. Open to everyone.")]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int id)
    {
        if (!await _db.Events.AnyAsync(e => e.EventID == id)) return Missing("Event not found.");

        var results = await ResultQuery()
            .Where(r => r.EventEntrance!.Category!.EventID == id)
            .ToListAsync();

        var ordered = results
            .OrderBy(r => r.EventEntrance!.Category!.CategoryName)
            .ThenBy(r => r.FinishTime)
            .Select(r => r.ToResponse());

        return Ok(ordered);
    }

    [HttpGet("category/{id:int}/result")]
    [SwaggerOperation(
        Summary = "Category results",
        Description = "Returns the results of one category ordered from the fastest to the slowest finish time. Open to everyone.")]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForCategory(int id)
    {
        if (!await _db.Categories.AnyAsync(c => c.CategoryID == id)) return Missing("Category not found.");

        var results = await ResultQuery()
            .Where(r => r.EventEntrance!.CategoryID == id)
            .ToListAsync();

        return Ok(results.OrderBy(r => r.FinishTime).Select(r => r.ToResponse()));
    }

    [HttpGet("participant/me/result")]
    [Authorize(Roles = Roles.Participant)]
    [SwaggerOperation(
        Summary = "My results",
        Description = "Returns the results of the logged-in Participant across all their entries. Participant only.")]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var participantId = CurrentUserId;

        var results = await ResultQuery()
            .Where(r => r.EventEntrance!.ParticipantID == participantId)
            .ToListAsync();

        return Ok(results.OrderBy(r => r.RecordedAt).Select(r => r.ToResponse()));
    }

    private IQueryable<Result> ResultQuery() =>
        _db.Results
            .Include(r => r.EventEntrance).ThenInclude(en => en!.Participant)
            .Include(r => r.EventEntrance).ThenInclude(en => en!.Category).ThenInclude(c => c!.Event);
}
