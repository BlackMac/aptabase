using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;

namespace Aptabase.Features.ReadApi;

public class HasValidReadApiTokenAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var env = context.HttpContext.RequestServices.GetService<EnvSettings>()
            ?? throw new InvalidOperationException("Could not resolve EnvSettings.");

        if (string.IsNullOrEmpty(env.ReadApiToken))
        {
            context.Result = new ObjectResult(new { error = "Reading API is disabled. Set READ_API_TOKEN to enable it." })
            {
                StatusCode = 503
            };
            return;
        }

        if (!TryGetBearerToken(context.HttpContext.Request.Headers.Authorization, out var provided)
            || !FixedTimeEquals(provided, env.ReadApiToken))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        base.OnActionExecuting(context);
    }

    private static bool TryGetBearerToken(StringValues authHeader, out string token)
    {
        token = "";
        if (authHeader.Count == 0) return false;
        var raw = authHeader.ToString();
        if (string.IsNullOrWhiteSpace(raw)) return false;
        const string prefix = "Bearer ";
        if (!raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        token = raw.Substring(prefix.Length).Trim();
        return token.Length > 0;
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
