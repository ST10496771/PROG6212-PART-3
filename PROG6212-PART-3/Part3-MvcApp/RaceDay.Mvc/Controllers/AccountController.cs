using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

public class AccountController : AppController
{
    public AccountController(ApiClient api) : base(api) { }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null, bool expired = false)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToHome();

        if (expired) FlashError("Your session has ended. Please log in again.");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        HttpContext.Session.Remove(ApiClient.SessionKey);   // never reuse an old API session

        var response = await Api.SendAsync<AccountDto>(HttpMethod.Post, "api/auth/login",
            new { email = model.Email.Trim(), password = model.Password }, requireSession: false);

        if (!response.Ok || response.Data == null)
        {
            ModelState.AddModelError(string.Empty,
                response.StatusCode == 401 ? "Incorrect email or password." : response.Message);
            return View(model);
        }

        var a = response.Data;
        await SignInUserAsync(a.Id, a.Role, a.FirstName, a.LastName, a.Email);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        return RedirectToHome();
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToHome();
        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        HttpContext.Session.Remove(ApiClient.SessionKey);

        var response = await Api.SendAsync<AccountDto>(HttpMethod.Post, "api/auth/register", new
        {
            role = model.Role,
            firstName = model.FirstName.Trim(),
            lastName = model.LastName.Trim(),
            email = model.Email.Trim(),
            password = model.Password,
            contactNumber = model.Role == "Organiser" ? model.ContactNumber : null
        }, requireSession: false);

        if (!response.Ok || response.Data == null)
        {
            ModelState.AddModelError(string.Empty,
                response.StatusCode == 409 ? "An account with that email address already exists." : response.Message);
            return View(model);
        }

        // The API logs the new user in as part of registration, so sign in here too.
        var a = response.Data;
        await SignInUserAsync(a.Id, a.Role, a.FirstName, a.LastName, a.Email);
        FlashSuccess($"Welcome to RaceDay, {a.FirstName}!");
        return RedirectToHome();
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await Api.SendAsync(HttpMethod.Post, "api/auth/logout");
        }
        catch (ApiSessionExpiredException)
        {
            // Already logged out on the API side - nothing more to do.
        }

        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        FlashSuccess("You have been logged out.");
        return RedirectToAction("Index", "Home");
    }

    // ---------- Profile picture (Organisers and Participants both have one) ----------

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> UploadPicture(IFormFile? picture)
    {
        var problem = picture == null || picture.Length == 0 ? "Please choose an image to upload." : ValidateImage(picture);
        if (problem != null)
        {
            FlashError(problem);
            return RedirectToProfile();
        }

        var response = await Api.UploadAsync<object>("api/profile/picture", picture!);
        if (response.Ok) FlashSuccess("Your profile picture has been updated.");
        else FlashError(response.Detail ?? response.Message);

        return RedirectToProfile();
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> RemovePicture()
    {
        var response = await Api.SendAsync(HttpMethod.Delete, "api/profile/picture");
        if (response.Ok) FlashSuccess("Your profile picture has been removed.");
        else FlashError(response.Message);

        return RedirectToProfile();
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = 403;
        return View();
    }

    private IActionResult RedirectToProfile()
        => User.IsInRole("Organiser") ? RedirectToAction("Profile", "Organiser") : RedirectToAction("Profile", "Participant");

    private IActionResult RedirectToHome()
        => User.IsInRole("Organiser") ? RedirectToAction("Dashboard", "Organiser") : RedirectToAction("Index", "Home");
}
