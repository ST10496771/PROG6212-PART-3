using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

public class HomeController : AppController
{
    private static readonly string[] EventTypes = { "Run", "Walk", "Cycle" };

    public HomeController(ApiClient api) : base(api) { }

    public async Task<IActionResult> Index(string? location, string? eventType, DateTime? date, bool includePast = false)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(location)) query.Add("location=" + Uri.EscapeDataString(location.Trim()));
        if (!string.IsNullOrWhiteSpace(eventType) && EventTypes.Contains(eventType)) query.Add("eventType=" + eventType);
        else eventType = null;
        if (date.HasValue) query.Add("date=" + date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        var url = "api/event" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        var response = await Api.GetAsync<List<EventDto>>(url);
        if (!response.Ok || response.Data == null) return Failure(response);

        // By default only upcoming events are shown; "Also show past events" includes the rest.
        var events = includePast
            ? response.Data
            : response.Data.Where(e => e.EventDate.Date >= DateTime.UtcNow.Date).ToList();

        return View(new HomeIndexViewModel
        {
            Events = events,
            Location = location,
            EventType = eventType,
            Date = date,
            IncludePast = includePast
        });
    }

    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? code)
    {
        var model = code switch
        {
            404 => new ErrorViewModel("Page not found", "We couldn't find the page you were looking for."),
            403 => new ErrorViewModel("Access denied", "You don't have permission to view that page."),
            _ => new ErrorViewModel("Something went wrong", "An unexpected problem occurred. Please try again in a moment.")
        };
        return View(model);
    }
}
