using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Helpers;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

[Authorize(Roles = "Participant")]
public class ParticipantController : AppController
{
    public ParticipantController(ApiClient api) : base(api) { }

    [HttpPost]
    public async Task<IActionResult> Enrol(int categoryId, int eventId)
    {
        var response = await Api.SendAsync<EntranceDto>(HttpMethod.Post, $"api/category/{categoryId}/evententrance");

        if (response.Ok)
        {
            FlashSuccess("You're enrolled! You can see all your entries below.");
            return RedirectToAction(nameof(Entries));
        }

        // 409 messages from the API are friendly and specific (already entered / another event on the same date).
        FlashError(response.Message);
        return RedirectToAction("Details", "Events", new { id = eventId });
    }

    [HttpGet]
    public async Task<IActionResult> Entries()
    {
        var entries = await Api.GetAsync<List<EntranceDto>>("api/participant/me/evententrance");
        if (!entries.Ok || entries.Data == null) return Failure(entries);

        var results = await Api.GetAsync<List<ResultDto>>("api/participant/me/result");
        var resultsByEntry = results.Ok && results.Data != null
            ? results.Data.ToDictionary(r => r.EventEntranceId)
            : new Dictionary<int, ResultDto>();

        var rows = entries.Data
            .Select(e => new EntryRow { Entry = e, Result = resultsByEntry.GetValueOrDefault(e.EventEntranceId) })
            .ToList();

        return View(rows);
    }

    [HttpPost]
    public async Task<IActionResult> Withdraw(int id)
    {
        var response = await Api.SendAsync(HttpMethod.Delete, $"api/evententrance/{id}");

        if (response.Ok) FlashSuccess("Your enrolment has been withdrawn.");
        else FlashError(response.StatusCode == 404 ? "That enrolment no longer exists." : response.Message);

        return RedirectToAction(nameof(Entries));
    }

    [HttpGet]
    public async Task<IActionResult> Results()
    {
        var mine = await Api.GetAsync<List<ResultDto>>("api/participant/me/result");
        if (!mine.Ok || mine.Data == null) return Failure(mine);

        // The entries tell us which event (and date) each result belongs to.
        var entries = await Api.GetAsync<List<EntranceDto>>("api/participant/me/evententrance");
        var entryById = entries.Ok && entries.Data != null
            ? entries.Data.ToDictionary(e => e.EventEntranceId)
            : new Dictionary<int, EntranceDto>();

        // For "47th out of 312" we need everyone's results in the same events.
        var eventIds = mine.Data
            .Select(r => entryById.GetValueOrDefault(r.EventEntranceId)?.EventId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var rankById = new Dictionary<int, ResultRow>();
        foreach (var eventId in eventIds)
        {
            var response = await Api.GetAsync<List<ResultDto>>($"api/event/{eventId}/result");
            if (!response.Ok || response.Data == null) continue;

            foreach (var group in ResultsHelper.GroupByCategory(response.Data))
                foreach (var row in group.Rows)
                    rankById[row.Result.ResultId] = row;
        }

        var rows = mine.Data
            .Select(r => new ParticipantResultRow
            {
                Result = r,
                Entry = entryById.GetValueOrDefault(r.EventEntranceId),
                Position = rankById.TryGetValue(r.ResultId, out var rank) ? rank.Position : r.FinishPosition,
                TotalFinishers = rankById.TryGetValue(r.ResultId, out var rank2) ? rank2.TotalInCategory : null
            })
            .OrderByDescending(r => r.Entry?.EventDate)
            .ToList();

        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var response = await Api.GetAsync<AccountDto>("api/participant/me");
        if (!response.Ok || response.Data == null) return Failure(response);

        var p = response.Data;
        return View(new ProfileViewModel { FirstName = p.FirstName, LastName = p.LastName, Email = p.Email, Role = p.Role });
    }

    [HttpPost]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        // Participants have no contact number, so ignore any validation on it.
        ModelState.Remove(nameof(model.ContactNumber));
        if (!ModelState.IsValid) return View(model);

        var response = await Api.SendAsync<AccountDto>(HttpMethod.Put, "api/participant/me",
            new { firstName = model.FirstName.Trim(), lastName = model.LastName.Trim() });

        if (!response.Ok || response.Data == null)
        {
            ModelState.AddModelError(string.Empty, response.Message);
            return View(model);
        }

        await RefreshDisplayNameAsync(response.Data.FirstName, response.Data.LastName);
        FlashSuccess("Your profile has been updated.");
        return RedirectToAction(nameof(Profile));
    }
}
