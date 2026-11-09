using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayApi.Data;
using RaceDayApi.Dtos;
using RaceDayApi.Models;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDayApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : RaceDayControllerBase
{
    private readonly RaceDayContext _db;

    public AuthController(RaceDayContext db)
    {
        _db = db;
    }

    [HttpPost("register")]
    [SwaggerOperation(
        Summary = "Register a new account",
        Description = "Creates an Organiser or Participant account. Expects role (Organiser or Participant), firstName, lastName, email, password (min 8 characters) and an optional contactNumber. The password is hashed before it is saved and the user is logged in straight away.")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var role = request.Role.Trim();

        bool isOrganiser = string.Equals(role, Roles.Organiser, StringComparison.OrdinalIgnoreCase);
        bool isParticipant = string.Equals(role, Roles.Participant, StringComparison.OrdinalIgnoreCase);

        if (!isOrganiser && !isParticipant)
        {
            return BadRequest(new { message = "Role must be either Organiser or Participant." });
        }

        var emailTaken = await _db.Organisers.AnyAsync(o => o.OrganiserEmailAddress == email)
            || await _db.Participants.AnyAsync(p => p.EmailAddress == email);

        if (emailTaken)
        {
            return Conflict(new { message = "That email address is already registered." });
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        AccountResponse account;

        if (isOrganiser)
        {
            var organiser = new Organiser
            {
                OrganiserFirstName = request.FirstName.Trim(),
                OrganiserLastName = request.LastName.Trim(),
                OrganiserEmailAddress = email,
                OrganiserContactNumber = request.ContactNumber,
                PasswordHash = hash
            };
            _db.Organisers.Add(organiser);
            await _db.SaveChangesAsync();
            account = new AccountResponse(organiser.OrganiserID, Roles.Organiser, organiser.OrganiserFirstName, organiser.OrganiserLastName, email, organiser.OrganiserContactNumber);
        }
        else
        {
            var participant = new Participant
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                EmailAddress = email,
                PasswordHash = hash
            };
            _db.Participants.Add(participant);
            await _db.SaveChangesAsync();
            account = new AccountResponse(participant.ParticipantID, Roles.Participant, participant.FirstName, participant.LastName, email, null);
        }

        await StartSession(account);
        return StatusCode(StatusCodes.Status201Created, account);
    }

    [HttpPost("login")]
    [SwaggerOperation(
        Summary = "Log in",
        Description = "Checks the email and password. If they are correct a session cookie is created that holds the user's id and role, and the account summary is returned.")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var organiser = await _db.Organisers.FirstOrDefaultAsync(o => o.OrganiserEmailAddress == email);
        if (organiser != null && BCrypt.Net.BCrypt.Verify(request.Password, organiser.PasswordHash))
        {
            var account = new AccountResponse(organiser.OrganiserID, Roles.Organiser, organiser.OrganiserFirstName, organiser.OrganiserLastName, email, organiser.OrganiserContactNumber);
            await StartSession(account);
            return Ok(account);
        }

        var participant = await _db.Participants.FirstOrDefaultAsync(p => p.EmailAddress == email);
        if (participant != null && BCrypt.Net.BCrypt.Verify(request.Password, participant.PasswordHash))
        {
            var account = new AccountResponse(participant.ParticipantID, Roles.Participant, participant.FirstName, participant.LastName, email, null);
            await StartSession(account);
            return Ok(account);
        }

        return Unauthorized(new { message = "Invalid email or password." });
    }

    [HttpPost("logout")]
    [Authorize]
    [SwaggerOperation(
        Summary = "Log out",
        Description = "Ends the current session by removing the session cookie.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    private async Task StartSession(AccountResponse account)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, $"{account.FirstName} {account.LastName}"),
            new(ClaimTypes.Email, account.Email),
            new(ClaimTypes.Role, account.Role)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
