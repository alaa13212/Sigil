using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;

namespace Sigil.Server.Framework;


public class TooManyRequestsResult(TimeSpan? retryAfter = null) : IActionResult
{
    private TimeSpan? RetryAfter { get; } = retryAfter;

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (RetryAfter.HasValue)
        {
            response.Headers.RetryAfter = new RetryConditionHeaderValue(RetryAfter.Value).ToString();
        }

        await response.CompleteAsync();
    }
}