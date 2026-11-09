using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDayApi.Data;
using RaceDayApi.Dtos;
using RaceDayApi.Models;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDayApi.Controllers;

[ApiController]
[Route("api")]
public class ProfileController : RaceDayControllerBase
{
    private readonly RaceDayContext _db;

    public ProfileController(RaceDayContext db)
    {
        _db = db;
    }

    [HttpGet("organiser/me")]
    [Authorize(Roles = Roles.Organiser)]
    [SwaggerOperation(
        Summary = "View my organiser profile",
        Description = "Returns the profile of the logged-in Organiser. Organiser only.")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrganiser()
    {
        var organiser = await _db.Organisers.FindAsync(CurrentUserId);
        if (organiser == null) return Missing("Organiser not found.");

        return Ok(ToAccount(organiser));
    }

    [HttpPut("organiser/me")]
    [Authorize(Roles = Roles.Organiser)]
    [SwaggerOperation(
        Summary = "Update my organiser profile",
        Description = "Updates the first name, last name and contact number of the logged-in Organiser. Organiser only.")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateOrganiser(UpdateOrganiserRequest request)
    {
        var organiser = await _db.Organisers.FindAsync(CurrentUserId);
        if (organiser == null) return Missing("Organiser not found.");

        organiser.OrganiserFirstName = request.FirstName.Trim();
        organiser.OrganiserLastName = request.LastName.Trim();
        organiser.OrganiserContactNumber = request.ContactNumber;
        await _db.SaveChangesAsync();

        return Ok(ToAccount(organiser));
    }

    [HttpGet("participant/me")]
    [Authorize(Roles = Roles.Participant)]
    [SwaggerOperation(
        Summary = "View my participant profile",
        Description = "Returns the profile of the logged-in Participant. Participant only.")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetParticipant()
    {
        var participant = await _db.Participants.FindAsync(CurrentUserId);
        if (participant == null) return Missing("Participant not found.");

        return Ok(ToAccount(participant));
    }

    [HttpPut("participant/me")]
    [Authorize(Roles = Roles.Participant)]
    [SwaggerOperation(
        Summary = "Update my participant profile",
        Description = "Updates the first name and last name of the logged-in Participant. Participant only.")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateParticipant(UpdateParticipantRequest request)
    {
        var participant = await _db.Participants.FindAsync(CurrentUserId);
        if (participant == null) return Missing("Participant not found.");

        participant.FirstName = request.FirstName.Trim();
        participant.LastName = request.LastName.Trim();
        await _db.SaveChangesAsync();

        return Ok(ToAccount(participant));
    }

    private static AccountResponse ToAccount(Organiser o) =>
        new(o.OrganiserID, Roles.Organiser, o.OrganiserFirstName, o.OrganiserLastName, o.OrganiserEmailAddress, o.OrganiserContactNumber);

    private static AccountResponse ToAccount(Participant p) =>
        new(p.ParticipantID, Roles.Participant, p.FirstName, p.LastName, p.EmailAddress, null);
}
