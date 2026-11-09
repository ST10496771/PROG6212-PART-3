using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Filters;

/// <summary>If the API says the session is no longer valid, log the user out of the MVC app and send them to the login page.</summary>
public class SessionExpiredFilter : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not ApiSessionExpiredException) return;

        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        context.HttpContext.Session.Clear();

        context.Result = new RedirectToActionResult("Login", "Account", new { expired = true });
        context.ExceptionHandled = true;
    }
}
