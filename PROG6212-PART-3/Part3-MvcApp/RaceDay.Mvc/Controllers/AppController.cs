using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

public abstract class AppController : Controller
{
    protected AppController(ApiClient api) => Api = api;

    protected ApiClient Api { get; }

    protected int CurrentUserId
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    protected void FlashSuccess(string message) => TempData["Success"] = message;
    protected void FlashError(string message) => TempData["Error"] = message;

    /// <summary>Turns a failed API call into a friendly page (never raw error text).</summary>
    protected IActionResult Failure(ApiResponse response)
    {
        switch (response.StatusCode)
        {
            case 403:
                return RedirectToAction("AccessDenied", "Account");
            case 404:
                Response.StatusCode = 404;
                return View("~/Views/Shared/NotFound.cshtml");
            default:
                return View("~/Views/Shared/ApiError.cshtml", (object)response.Message);
        }
    }

    /// <summary>Optional image check done in the MVC app so users get quick feedback; the API validates again.</summary>
    protected static string? ValidateImage(IFormFile? file)
    {
        if (file == null || file.Length == 0) return null;
        if (file.Length > 5 * 1024 * 1024) return "The image must be 5 MB or smaller.";

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".gif" or ".webp"))
            return "Only JPG, PNG, GIF or WEBP images are allowed.";

        return null;
    }

    protected async Task SignInUserAsync(int id, string role, string firstName, string lastName, string email)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id.ToString()),
            new(ClaimTypes.Name, $"{firstName} {lastName}"),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    /// <summary>Keeps the name in the navigation bar up to date after a profile change.</summary>
    protected async Task RefreshDisplayNameAsync(string firstName, string lastName)
    {
        var claims = User.Claims.Where(c => c.Type != ClaimTypes.Name).ToList();
        claims.Add(new Claim(ClaimTypes.Name, $"{firstName} {lastName}"));
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
