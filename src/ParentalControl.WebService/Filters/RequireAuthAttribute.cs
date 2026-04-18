using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ParentalControl.WebService.Services;

namespace ParentalControl.WebService.Filters;

public class RequireAuthAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var authService = context.HttpContext.RequestServices.GetService<AuthService>();
        
        if (authService == null || !authService.IsAuthenticated())
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Authentication required" });
            return;
        }

        base.OnActionExecuting(context);
    }
}
