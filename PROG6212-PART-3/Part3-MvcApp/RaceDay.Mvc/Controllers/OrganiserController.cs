using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

[Authorize(Roles = "Organiser")]
public class OrganiserController : AppController
{
    public OrganiserController(ApiClient api) : base(api) { }

    // ---------- Dashboard & events list ----------

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var rows = await LoadMyEventRowsAsync();
        if (rows == null) return View("~/Views/Shared/ApiError.cshtml", (object)"We couldn't load your events. Please try again.");
        return View(new DashboardViewModel { Events = rows });
    }

    [HttpGet]
    public async Task<IActionResult> Events()
    {
        var rows = await LoadMyEventRowsAsync();
        if (rows == null) return View("~/Views/Shared/ApiError.cshtml", (object)"We couldn't load your events. Please try again.");
        return View(rows);
    }

    // ---------- Create / edit / delete event ----------

    [HttpGet]
    public IActionResult CreateEvent() => View("EventForm", new EventFormViewModel());

    [HttpPost]
    public async Task<IActionResult> CreateEvent(EventFormViewModel model)
    {
        var imageProblem = ValidateImage(model.BannerFile);
        if (imageProblem != null) ModelState.AddModelError(nameof(model.BannerFile), imageProblem);
        if (!ModelState.IsValid) return View("EventForm", model);

        var created = await Api.SendAsync<EventDto>(HttpMethod.Post, "api/event", ToRequest(model));
        if (!created.Ok || created.Data == null)
        {
            ModelState.AddModelError(string.Empty, created.Message);
            return View("EventForm", model);
        }

        if (model.BannerFile is { Length: > 0 })
        {
            var upload = await Api.UploadAsync<object>($"api/event/{created.Data.EventId}/banner", model.BannerFile);
            if (!upload.Ok)
            {
                FlashError($"The event was created, but the banner could not be uploaded: {upload.Detail ?? upload.Message}");
                return RedirectToAction(nameof(EditEvent), new { id = created.Data.EventId });
            }
        }

        FlashSuccess($"\"{created.Data.EventName}\" has been created. Now add some categories.");
        return RedirectToAction(nameof(Categories), new { id = created.Data.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var (ev, failure) = await LoadOwnedEventAsync(id);
        if (ev == null) return failure!;

        return View("EventForm", new EventFormViewModel
        {
            EventId = ev.EventId,
            EventName = ev.EventName,
            EventDescription = ev.EventDescription,
            EventDate = ev.EventDate.Date,
            Location = ev.Location,
            DistanceKm = ev.DistanceKm,
            EventType = ev.EventType
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditEvent(int id, EventFormViewModel model)
    {
        model.EventId = id;

        var imageProblem = ValidateImage(model.BannerFile);
        if (imageProblem != null) ModelState.AddModelError(nameof(model.BannerFile), imageProblem);
        if (!ModelState.IsValid) return View("EventForm", model);

        var updated = await Api.SendAsync<EventDto>(HttpMethod.Put, $"api/event/{id}", ToRequest(model));
        if (!updated.Ok)
        {
            if (updated.StatusCode is 403 or 404) return Failure(updated);
            ModelState.AddModelError(string.Empty, updated.Message);
            return View("EventForm", model);
        }

        var bannerMessage = string.Empty;

        if (model.BannerFile is { Length: > 0 })
        {
            var upload = await Api.UploadAsync<object>($"api/event/{id}/banner", model.BannerFile);
            if (!upload.Ok) bannerMessage = $" The banner could not be uploaded: {upload.Detail ?? upload.Message}";
        }
        else if (model.RemoveBanner)
        {
            var removed = await Api.SendAsync(HttpMethod.Delete, $"api/event/{id}/banner");
            if (!removed.Ok) bannerMessage = $" The banner could not be removed: {removed.Message}";
        }

        if (bannerMessage.Length > 0) FlashError("Event details saved." + bannerMessage);
        else FlashSuccess("Event updated.");

        return RedirectToAction(nameof(Events));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var response = await Api.SendAsync(HttpMethod.Delete, $"api/event/{id}");

        if (response.Ok) FlashSuccess("The event and everything linked to it has been deleted.");
        else FlashError(response.Message);

        return RedirectToAction(nameof(Events));
    }

    // ---------- Categories ----------

    [HttpGet]
    public async Task<IActionResult> Categories(int id)
    {
        var (ev, failure) = await LoadOwnedEventAsync(id);
        if (ev == null) return failure!;

        var categories = await Api.GetAsync<List<CategoryDto>>($"api/event/{id}/category");
        if (!categories.Ok || categories.Data == null) return Failure(categories);

        return View(new CategoriesViewModel { Event = ev, Categories = categories.Data });
    }

    [HttpPost]
    public async Task<IActionResult> AddCategory(int id, string? categoryName, string? categoryDescription)
    {
        var name = categoryName?.Trim();
        var description = string.IsNullOrWhiteSpace(categoryDescription) ? null : categoryDescription.Trim();

        if (string.IsNullOrEmpty(name) || name.Length > 100)
        {
            FlashError("Please enter a category name of up to 100 characters.");
            return RedirectToAction(nameof(Categories), new { id });
        }
        if (description is { Length: > 255 })
        {
            FlashError("The description can be at most 255 characters.");
            return RedirectToAction(nameof(Categories), new { id });
        }

        var response = await Api.SendAsync<CategoryDto>(HttpMethod.Post, $"api/event/{id}/category",
            new { categoryName = name, categoryDescription = description });

        if (response.Ok) FlashSuccess($"Category \"{name}\" added.");
        else FlashError(response.Message);

        return RedirectToAction(nameof(Categories), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int id, int categoryId)
    {
        var response = await Api.SendAsync(HttpMethod.Delete, $"api/category/{categoryId}");

        if (response.Ok) FlashSuccess("Category removed.");
        else FlashError(response.Message);

        return RedirectToAction(nameof(Categories), new { id });
    }

    // ---------- Enrolments ----------

    [HttpGet]
    public async Task<IActionResult> Enrolments(int id)
    {
        var (ev, failure) = await LoadOwnedEventAsync(id);
        if (ev == null) return failure!;

        var rows = await LoadEntryRowsAsync(id);
        if (rows == null) return View("~/Views/Shared/ApiError.cshtml", (object)"We couldn't load the enrolments. Please try again.");

        return View(new EventEntriesViewModel { Event = ev, Entries = rows });
    }

    // ---------- Results ----------

    [HttpGet]
    public async Task<IActionResult> Results(int id)
    {
        var (ev, failure) = await LoadOwnedEventAsync(id);
        if (ev == null) return failure!;

        var rows = await LoadEntryRowsAsync(id);
        if (rows == null) return View("~/Views/Shared/ApiError.cshtml", (object)"We couldn't load the enrolments. Please try again.");

        return View(new EventEntriesViewModel { Event = ev, Entries = rows });
    }

    [HttpPost]
    public async Task<IActionResult> CaptureResult(int id, int entryId, string? finishTime, int? finishPosition)
    {
        var formats = new[] { @"h\:mm\:ss", @"hh\:mm\:ss" };
        if (string.IsNullOrWhiteSpace(finishTime) ||
            !TimeSpan.TryParseExact(finishTime.Trim(), formats, CultureInfo.InvariantCulture, out var time) ||
            time <= TimeSpan.Zero)
        {
            FlashError("Please enter the finish time as hours:minutes:seconds, for example 01:45:30.");
            return RedirectToAction(nameof(Results), new { id });
        }

        if (finishPosition is < 1 or > 100000)
        {
            FlashError("The finishing position must be a number from 1 upwards, or left empty.");
            return RedirectToAction(nameof(Results), new { id });
        }

        var response = await Api.SendAsync<ResultDto>(HttpMethod.Post, $"api/evententrance/{entryId}/result",
            new { finishTime = time.ToString("c", CultureInfo.InvariantCulture), finishPosition });

        if (response.Ok) FlashSuccess("Result saved and published.");
        else FlashError(response.Message);

        return RedirectToAction(nameof(Results), new { id });
    }

    // ---------- Profile ----------

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var response = await Api.GetAsync<AccountDto>("api/organiser/me");
        if (!response.Ok || response.Data == null) return Failure(response);

        var o = response.Data;
        return View(new ProfileViewModel
        {
            FirstName = o.FirstName,
            LastName = o.LastName,
            Email = o.Email,
            ContactNumber = o.ContactNumber,
            Role = o.Role
        });
    }

    [HttpPost]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var response = await Api.SendAsync<AccountDto>(HttpMethod.Put, "api/organiser/me", new
        {
            firstName = model.FirstName.Trim(),
            lastName = model.LastName.Trim(),
            contactNumber = model.ContactNumber
        });

        if (!response.Ok || response.Data == null)
        {
            ModelState.AddModelError(string.Empty, response.Message);
            return View(model);
        }

        await RefreshDisplayNameAsync(response.Data.FirstName, response.Data.LastName);
        FlashSuccess("Your profile has been updated.");
        return RedirectToAction(nameof(Profile));
    }

    // ---------- Helpers ----------

    /// <summary>The API lists all events, so keep only the ones owned by the logged-in Organiser and add the counts.</summary>
    private async Task<List<OrganiserEventRow>?> LoadMyEventRowsAsync()
    {
        var all = await Api.GetAsync<List<EventDto>>("api/event");
        if (!all.Ok || all.Data == null) return null;

        var mine = all.Data.Where(e => e.OrganiserId == CurrentUserId).OrderBy(e => e.EventDate).ToList();

        // One request after another: the API session is shared, so the calls are not made in parallel.
        var rows = new List<OrganiserEventRow>();
        foreach (var e in mine)
        {
            var categories = await Api.GetAsync<List<CategoryDto>>($"api/event/{e.EventId}/category");
            var entries = await Api.GetAsync<List<EntranceDto>>($"api/event/{e.EventId}/evententrance");
            var results = await Api.GetAsync<List<ResultDto>>($"api/event/{e.EventId}/result");

            rows.Add(new OrganiserEventRow
            {
                Event = e,
                CategoryCount = categories.Data?.Count ?? 0,
                EnrolmentCount = entries.Data?.Count ?? 0,
                ResultsCount = results.Data?.Count ?? 0
            });
        }

        return rows;
    }

    /// <summary>Loads every enrolment for an event together with its result, so the status can be shown.</summary>
    private async Task<List<EntryRow>?> LoadEntryRowsAsync(int eventId)
    {
        var entries = await Api.GetAsync<List<EntranceDto>>($"api/event/{eventId}/evententrance");
        if (!entries.Ok || entries.Data == null) return null;

        var results = await Api.GetAsync<List<ResultDto>>($"api/event/{eventId}/result");
        var byEntry = results.Ok && results.Data != null
            ? results.Data.ToDictionary(r => r.EventEntranceId)
            : new Dictionary<int, ResultDto>();

        return entries.Data
            .Select(e => new EntryRow { Entry = e, Result = byEntry.GetValueOrDefault(e.EventEntranceId) })
            .ToList();
    }

    /// <summary>Loads an event and makes sure the logged-in Organiser owns it.</summary>
    private async Task<(EventDto? Event, IActionResult? Failure)> LoadOwnedEventAsync(int id)
    {
        var response = await Api.GetAsync<EventDto>($"api/event/{id}");
        if (!response.Ok || response.Data == null) return (null, Failure(response));

        if (response.Data.OrganiserId != CurrentUserId)
            return (null, RedirectToAction("AccessDenied", "Account"));

        return (response.Data, null);
    }

    private static object ToRequest(EventFormViewModel m) => new
    {
        eventName = m.EventName.Trim(),
        eventDescription = m.EventDescription,
        eventDate = m.EventDate!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        location = m.Location.Trim(),
        distanceKm = m.DistanceKm,
        eventType = m.EventType
    };
}
