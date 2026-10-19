#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using NMBHLabSys.LabManagerSubsystem.Data;
using NMBHLabSys.LabManagerSubsystem.Services;

namespace NMBHLabSys.LabManagerSubsystem.Infrastructure;

/// <summary>Put on an action to let people in without being logged in as a manager (Forgot Password etc.).</summary>
[AttributeUsage(AttributeTargets.Method)]
public class LabManagerPublicAttribute : Attribute { }

/// <summary>Put on the controller: every action then runs LabManagerAccessFilter first.</summary>
public class LabManagerOnlyAttribute : TypeFilterAttribute
{
    public LabManagerOnlyAttribute() : base(typeof(LabManagerAccessFilter)) { }
}

/// <summary>
/// Role-based access control for every Lab Manager page (AC-09-07 / TC-LM-09-06):
///   * nobody logged in              -> the group's login page
///   * logged in, but not a manager (no LabManager row) -> AccessDenied
///   * account deactivated ([User].IsActive = 0)       -> the login page
///   * manager still on a temporary password -> ChangePassword (they can't skip it by typing a URL)
/// It also stops the browser caching manager pages, so after Logout the Back button
/// can't show them again (TC-LM-09-05).
/// </summary>
public class LabManagerAccessFilter : IAsyncActionFilter
{
    private readonly ILabManagerCurrentUser _currentUser;
    private readonly ILabManagerData _data;
    private readonly LabManagerAuthOptions _auth;

    // Actions a manager with a temporary password may still open
    private static readonly string[] AllowedWhileMustChange = { "ChangePassword", "Logout" };

    public LabManagerAccessFilter(ILabManagerCurrentUser currentUser, ILabManagerData data, IOptions<LabManagerOptions> options)
    {
        _currentUser = currentUser;
        _data = data;
        _auth = options.Value.Auth;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var response = context.HttpContext.Response;
        response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        response.Headers["Pragma"] = "no-cache";
        response.Headers["Expires"] = "0";

        var action = context.ActionDescriptor as ControllerActionDescriptor;
        bool isPublic = action?.MethodInfo.GetCustomAttributes(typeof(LabManagerPublicAttribute), true).Any() == true;
        if (isPublic)
        {
            await next();
            return;
        }

        int? userId = _currentUser.UserId;
        if (userId is null)
        {
            context.Result = new RedirectResult(_auth.LoginPath);
            return;
        }

        var user = await _data.Accounts.GetUserByIdAsync(userId.Value);
        if (user is null || !user.IsActive)
        {
            // The session points at a user that no longer exists or was deactivated
            _currentUser.SignOut();
            context.Result = new RedirectResult(_auth.LoginPath);
            return;
        }

        // grp-04-14 has no Role column: a laboratory manager is a user with a row in the LabManager table
        if (user.LabManagerId is null)
        {
            context.Result = new RedirectToActionResult("AccessDenied", "LabManager", null);
            return;
        }

        if (user.MustChangePassword && !AllowedWhileMustChange.Contains(action?.ActionName, StringComparer.OrdinalIgnoreCase))
        {
            context.Result = new RedirectToActionResult("ChangePassword", "LabManager", null);
            return;
        }

        // The sidebar can show this instead of the hard-coded name: @ViewData["LabManagerName"]
        if (context.Controller is Controller controller)
        {
            controller.ViewData["LabManagerName"] = string.IsNullOrEmpty(user.FullName) ? user.Email : user.FullName;
            controller.ViewData["LabManagerUserId"] = user.UserId;
        }

        await next();
    }
}
