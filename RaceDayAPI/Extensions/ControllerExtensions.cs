using Microsoft.AspNetCore.Mvc;

namespace RaceDayApi.Extensions;

public static class ControllerExtensions
{
    /// <summary>A friendly 503 response used when Azure Blob Storage cannot be reached or is not configured.</summary>
    public static ObjectResult StorageUnavailable(this ControllerBase controller)
        => controller.StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
        {
            Title = "Image storage is currently unavailable. Please try again later.",
            Status = StatusCodes.Status503ServiceUnavailable
        });
}
