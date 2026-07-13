using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using ParentalControl.Shared.DTOs;
using ParentalControl.WebService.Data;

namespace ParentalControl.WebService.Filters;

// Validates the X-Api-Key header on /api/client/* endpoints against the calling
// computer's stored ApiKey (issued at registration). Deliberately NOT applied to
// Register itself -- registration must never be refused (this deployment's threat
// model is a curious child on the protected home network, not an internet attacker;
// see the product goals in work/fable-instructions.md).
//
// Grace mode (ParentalControl:RequireClientApiKey, default false): a MISSING key is
// logged and allowed, so already-deployed clients that predate this check keep working
// without a synchronized rollout. A PRESENT-BUT-WRONG key is always rejected, in both
// modes -- once every client has upgraded, the parent flips the setting to true to
// close the gap.
public class RequireClientApiKeyAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Register is the one endpoint that must always be reachable with no key at all
        // (that's how a computer gets its key in the first place). Checking the action
        // name here -- rather than requiring every controller author to remember to
        // annotate new actions -- means this filter is safe to apply once at the
        // controller level and stay secure-by-default for anything added later.
        var actionName = context.ActionDescriptor.RouteValues.TryGetValue("action", out var action) ? action : null;
        if (string.Equals(actionName, "Register", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        var services = context.HttpContext.RequestServices;
        var configuration = services.GetRequiredService<IConfiguration>();
        var db = services.GetRequiredService<AppDbContext>();
        var logger = services.GetRequiredService<ILogger<RequireClientApiKeyAttribute>>();

        var requireKey = configuration.GetValue("ParentalControl:RequireClientApiKey", false);
        var providedKey = context.HttpContext.Request.Headers["X-Api-Key"].FirstOrDefault();

        if (string.IsNullOrEmpty(providedKey))
        {
            if (requireKey)
            {
                context.Result = new UnauthorizedObjectResult(new { error = "X-Api-Key header required" });
                return;
            }

            logger.LogWarning("Client request to {Action} with no X-Api-Key (grace mode active, allowing)",
                context.ActionDescriptor.DisplayName);
            await next();
            return;
        }

        var computerId = await ResolveComputerIdAsync(context, db);
        if (computerId == null)
        {
            logger.LogWarning("Could not resolve computer for {Action}, cannot validate X-Api-Key", context.ActionDescriptor.DisplayName);
            if (requireKey)
            {
                context.Result = new UnauthorizedObjectResult(new { error = "Cannot resolve computer for key validation" });
                return;
            }
            await next();
            return;
        }

        var computer = await db.Computers.FindAsync(computerId.Value);
        if (computer?.ApiKey == null || !FixedTimeEquals(computer.ApiKey, providedKey!))
        {
            logger.LogWarning("Invalid X-Api-Key for computer {ComputerId} on {Action}", computerId, context.ActionDescriptor.DisplayName);
            context.Result = new UnauthorizedObjectResult(new { error = "Invalid API key" });
            return;
        }

        await next();
    }

    private static async Task<Guid?> ResolveComputerIdAsync(ActionExecutingContext context, AppDbContext db)
    {
        if (context.RouteData.Values.TryGetValue("computerId", out var routeValue) &&
            Guid.TryParse(routeValue?.ToString(), out var routeGuid))
        {
            return routeGuid;
        }

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is IHasComputerId hasComputerId)
                return hasComputerId.ComputerId;
        }

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is SessionEndRequest endRequest)
            {
                var session = await db.Sessions.FindAsync(endRequest.SessionId);
                if (session != null) return session.ComputerId;
            }
        }

        return null;
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
