using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace RaceDayApi.Controllers;

public abstract class RaceDayControllerBase : ControllerBase
{
    protected int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected IActionResult NotOwner(string message = "You do not own this resource.") =>
        StatusCode(StatusCodes.Status403Forbidden, new { message });

    protected IActionResult Missing(string message) =>
        NotFound(new { message });
}
