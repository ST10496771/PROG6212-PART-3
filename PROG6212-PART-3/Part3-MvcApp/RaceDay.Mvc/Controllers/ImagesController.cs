using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

/// <summary>Serves images by asking the RaceDay API - the MVC app never talks to Azure directly.</summary>
public class ImagesController : AppController
{
    public ImagesController(ApiClient api) : base(api) { }

    [HttpGet]
    public async Task<IActionResult> EventBanner(int id)
    {
        var image = await Api.GetImageAsync($"api/event/{id}/banner");
        if (image == null) return NotFound();

        Response.Headers.CacheControl = "public, max-age=30";
        return File(image.Value.Bytes, image.Value.ContentType);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ProfilePicture()
    {
        var image = await Api.GetImageAsync("api/profile/picture");
        if (image == null) return NotFound();

        Response.Headers.CacheControl = "private, no-store";
        return File(image.Value.Bytes, image.Value.ContentType);
    }
}
