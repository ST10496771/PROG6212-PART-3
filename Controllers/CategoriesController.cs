using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayApi.Data;
using RaceDayApi.Dtos;
using RaceDayApi.Models;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDayApi.Controllers;

[ApiController]
[Route("api")]
public class CategoriesController : RaceDayControllerBase
{
    private readonly RaceDayContext _db;

    public CategoriesController(RaceDayContext db)
    {
        _db = db;
    }

    [HttpGet("event/{eventId:int}/category")]
    [SwaggerOperation(
        Summary = "List categories for an event",
        Description = "Returns all age or distance categories that belong to the event. Open to everyone.")]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventID == eventId)) return Missing("Event not found.");

        var categories = await _db.Categories.Where(c => c.EventID == eventId).OrderBy(c => c.CategoryName).ToListAsync();
        return Ok(categories.Select(c => c.ToResponse()));
    }

    [HttpPost("event/{eventId:int}/category")]
    [Authorize(Roles = Roles.Organiser)]
    [SwaggerOperation(
        Summary = "Add a category to an event",
        Description = "Creates a category such as Under 20, Senior, 10km or 21km. Expects categoryName and an optional categoryDescription. Only the Organiser who owns the event can do this.")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(int eventId, CategoryRequest request)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev == null) return Missing("Event not found.");
        if (ev.OrganiserID != CurrentUserId) return NotOwner("You can only add categories to your own events.");

        var category = new Category
        {
            CategoryName = request.CategoryName.Trim(),
            CategoryDescription = request.CategoryDescription,
            EventID = eventId
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, category.ToResponse());
    }

    [HttpPut("category/{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [SwaggerOperation(
        Summary = "Update a category",
        Description = "Changes the name and description of a category. Only the Organiser who owns the event can do this.")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category == null) return Missing("Category not found.");
        if (category.Event!.OrganiserID != CurrentUserId) return NotOwner("You can only change categories on your own events.");

        category.CategoryName = request.CategoryName.Trim();
        category.CategoryDescription = request.CategoryDescription;
        await _db.SaveChangesAsync();

        return Ok(category.ToResponse());
    }

    [HttpDelete("category/{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [SwaggerOperation(
        Summary = "Delete a category",
        Description = "Removes a category and any enrolments in it. Only the Organiser who owns the event can do this.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category == null) return Missing("Category not found.");
        if (category.Event!.OrganiserID != CurrentUserId) return NotOwner("You can only delete categories on your own events.");

        var entries = await _db.EventEntrances.Where(en => en.CategoryID == id).ToListAsync();
        _db.EventEntrances.RemoveRange(entries);
        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
