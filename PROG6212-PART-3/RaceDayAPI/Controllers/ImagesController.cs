using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayApi.Data;
using RaceDayApi.Extensions;
using RaceDayApi.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDayApi.Controllers;

/// <summary>Event banners and profile pictures. The images live in Azure Blob Storage and are only served through this API.</summary>
[ApiController]
[Route("api")]
public class ImagesController : RaceDayControllerBase
{
    private readonly RaceDayContext _db;
    private readonly IBlobStorageService _storage;
    private readonly ILogger<ImagesController> _logger;

    public ImagesController(RaceDayContext db, IBlobStorageService storage, ILogger<ImagesController> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    // ---------- Event banners ----------

    [HttpPost("event/{id:int}/banner")]
    [Authorize(Roles = Roles.Organiser)]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        Summary = "Upload an event banner",
        Description = "Uploads (or replaces) the banner image of an event in Azure Blob Storage. Send multipart/form-data with one file field called file. JPG, PNG, GIF or WEBP, 5 MB at most. Only the Organiser who owns the event can do this.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadBanner(int id, IFormFile file)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return Missing("Event not found.");
        if (ev.OrganiserID != CurrentUserId) return NotOwner("You can only change the banner of your own events.");

        return await SaveImageAsync(file, BlobNames.EventBanner(id), "Banner uploaded.");
    }

    [HttpGet("event/{id:int}/banner")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Get an event banner",
        Description = "Returns the banner image of an event. Open to everyone. Returns 404 if the event has no banner.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> GetBanner(int id) => ReadImageAsync(BlobNames.EventBanner(id));

    [HttpDelete("event/{id:int}/banner")]
    [Authorize(Roles = Roles.Organiser)]
    [SwaggerOperation(
        Summary = "Remove an event banner",
        Description = "Deletes the banner image of an event. Only the Organiser who owns the event can do this.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteBanner(int id)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return Missing("Event not found.");
        if (ev.OrganiserID != CurrentUserId) return NotOwner("You can only change the banner of your own events.");

        return await RemoveImageAsync(BlobNames.EventBanner(id));
    }

    // ---------- Profile pictures (Organiser or Participant, each user has their own) ----------

    [HttpPost("profile/picture")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        Summary = "Upload my profile picture",
        Description = "Uploads (or replaces) the profile picture of the logged-in user in Azure Blob Storage. Send multipart/form-data with one file field called file. JPG, PNG, GIF or WEBP, 5 MB at most.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> UploadPicture(IFormFile file)
        => SaveImageAsync(file, ProfileBlobName(), "Profile picture uploaded.");

    [HttpGet("profile/picture")]
    [Authorize]
    [SwaggerOperation(
        Summary = "Get my profile picture",
        Description = "Returns the profile picture of the logged-in user. Returns 404 if none has been uploaded.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> GetPicture() => ReadImageAsync(ProfileBlobName());

    [HttpDelete("profile/picture")]
    [Authorize]
    [SwaggerOperation(
        Summary = "Remove my profile picture",
        Description = "Deletes the profile picture of the logged-in user.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> DeletePicture() => RemoveImageAsync(ProfileBlobName());

    // ---------- Helpers ----------

    private string ProfileBlobName()
        => BlobNames.ProfilePicture(User.FindFirstValue(ClaimTypes.Role) ?? "user", CurrentUserId);

    private async Task<IActionResult> SaveImageAsync(IFormFile? file, string blobName, string message)
    {
        var error = ImageUpload.Validate(file, out var contentType);
        if (error != null)
        {
            ModelState.AddModelError("file", error);
            return ValidationProblem(ModelState);
        }

        try
        {
            await using var stream = file!.OpenReadStream();
            await _storage.UploadAsync(blobName, stream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image upload failed for {BlobName}", blobName);
            return this.StorageUnavailable();
        }

        return Ok(new { message });
    }

    private async Task<IActionResult> ReadImageAsync(string blobName)
    {
        BlobContent? blob;
        try
        {
            blob = await _storage.DownloadAsync(blobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image download failed for {BlobName}", blobName);
            return this.StorageUnavailable();
        }

        if (blob == null) return Missing("No image has been uploaded.");
        return File(blob.Content, blob.ContentType);
    }

    private async Task<IActionResult> RemoveImageAsync(string blobName)
    {
        try
        {
            await _storage.DeleteAsync(blobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image delete failed for {BlobName}", blobName);
            return this.StorageUnavailable();
        }

        return NoContent();
    }
}
