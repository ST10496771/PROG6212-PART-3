using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Helpers;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

public class EventsController : AppController
{
    public EventsController(ApiClient api) : base(api) { }

    public async Task<IActionResult> Details(int id)
    {
        var ev = await Api.GetAsync<EventDto>($"api/event/{id}");
        if (!ev.Ok || ev.Data == null) return Failure(ev);

        var model = new EventDetailsViewModel
        {
            Event = ev.Data,
            IsOwner = User.IsInRole("Organiser") && ev.Data.OrganiserId == CurrentUserId
        };

        // Categories and results are shown on this page - if one of them can't be loaded the rest of the page still works.
        var categories = await Api.GetAsync<List<CategoryDto>>($"api/event/{id}/category");
        if (categories.Ok && categories.Data != null) model.Categories = categories.Data;

        var results = await Api.GetAsync<List<ResultDto>>($"api/event/{id}/result");
        if (results.Ok && results.Data != null) model.Results = ResultsHelper.GroupByCategory(results.Data);

        if (User.IsInRole("Participant"))
        {
            var mine = await Api.GetAsync<List<EntranceDto>>("api/participant/me/evententrance");
            if (mine.Ok && mine.Data != null)
                model.EnrolledCategoryIds = mine.Data.Select(e => e.CategoryId).ToHashSet();
        }

        return View(model);
    }
}
