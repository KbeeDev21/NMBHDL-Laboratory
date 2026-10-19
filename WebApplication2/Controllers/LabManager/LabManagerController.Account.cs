#nullable enable
using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NMBHLabSys.LabManagerSubsystem.Infrastructure;
using NMBHLabSys.LabManagerSubsystem.Models;
using NMBHLabSys.LabManagerSubsystem.Services;

namespace NMBHLabSys.LabManagerSubsystem.Controllers;

public partial class LabManagerController
{
    // =================================================================
    // US-LM-09  Login and Security  +  Profile pages
    // =================================================================

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var profile = await _account.GetProfileAsync(CurrentUserId);
        return profile is null ? RedirectToAction(nameof(Logout)) : View(profile);
    }

    [HttpGet]
    public async Task<IActionResult> EditProfile()
    {
        var form = await _account.GetEditProfileAsync(CurrentUserId);
        return form is null ? RedirectToAction(nameof(Logout)) : View(form);
    }

    [HttpPost]
    public async Task<IActionResult> EditProfile(EditProfileModel form)
    {
        var result = await _account.EditProfileAsync(CurrentUserId, form);
        if (!result.Succeeded) { ShowErrors(result); return View(form); }

        // Profile.cshtml has no message banner, so no TempData here (it would pop up on a later page)
        return RedirectToAction(nameof(Profile));
    }

    // ChangePassword.cshtml. Also where a manager still on a temporary password is sent.
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordModel());

    // AC-09-04 / TC-LM-09-04
    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordModel form)
    {
        var result = await _account.ChangePasswordAsync(CurrentUserId, form);
        if (result.NotFound) return RedirectToAction(nameof(Logout));
        if (!result.Succeeded) { ShowErrors(result); return View(new ChangePasswordModel()); }   // never send passwords back

        Flash(result);                       // the dashboard shows TempData, the profile page doesn't
        return RedirectToAction(nameof(Dashboard));
    }

    // ForgotPassword.cshtml (no login needed)
    [HttpGet, LabManagerPublic]
    public IActionResult ForgotPassword() => View(new ForgotPasswordModel());

    // AC-09-03 / TC-LM-09-03
    [HttpPost, LabManagerPublic]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordModel form)
    {
        var result = await _account.RequestPasswordResetAsync(form,
            token => Url.Action(nameof(ResetPassword), "LabManager", new { token }, Request.Scheme)!);

        if (!result.Succeeded && result.Errors.Exists(e => e.Field != "")) { ShowErrors(result); return View(form); }

        Flash(result);
        return RedirectToAction(nameof(ForgotPassword));
    }

    // ResetPassword.cshtml, opened from the emailed link: /LabManager/ResetPassword?token=...
    [HttpGet, LabManagerPublic]
    public async Task<IActionResult> ResetPassword(string? token)
    {
        if (!await _account.IsResetTokenValidAsync(token))
        {
            SetError(LabManagerAccountService.ResetLinkInvalidMessage);
            return RedirectToAction(nameof(ForgotPassword));
        }

        // The page's hidden ResetToken field is empty until the view is bound to the model,
        // so the token is also kept (encrypted) in TempData for the POST.
        TempData[ResetTokenKey] = token;
        return View(new ResetPasswordModel { ResetToken = token });
    }

    [HttpPost, LabManagerPublic]
    public async Task<IActionResult> ResetPassword(ResetPasswordModel form)
    {
        string? token = !string.IsNullOrWhiteSpace(form.ResetToken) ? form.ResetToken : TempData[ResetTokenKey] as string;

        var result = await _account.ResetPasswordAsync(token, form);
        if (result.NotFound) { Flash(result); return RedirectToAction(nameof(ForgotPassword)); }
        if (!result.Succeeded)
        {
            ShowErrors(result);
            TempData[ResetTokenKey] = token;   // keep it for the next attempt
            return View(new ResetPasswordModel { ResetToken = token });
        }

        Flash(result);
        return Redirect(_options.Auth.LoginPath);
    }

    private const string ResetTokenKey = "LabManagerResetToken";

    // AccessDenied.cshtml (AC-09-07 / TC-LM-09-06)
    [HttpGet, LabManagerPublic]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = (int)HttpStatusCode.Forbidden;
        return View();
    }

    // AC-09-05 / TC-LM-09-05: end the session and go to the login page
    [HttpGet, HttpPost, LabManagerPublic]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout()
    {
        _currentUser.SignOut();

        // If the group uses cookie authentication, sign that out too
        if (HttpContext.RequestServices.GetService<IAuthenticationSchemeProvider>() is { } schemes
            && await schemes.GetDefaultSignOutSchemeAsync() is not null)
        {
            await HttpContext.SignOutAsync();
        }

        return Redirect(_options.Auth.LoginPath);
    }

    // =================================================================
    // DEVELOPMENT ONLY: sign in without the group's login page.
    //   /LabManager/DevSignIn  (404 unless the app runs in Development)
    // Uses the real password check, so "Invalid credentials" (TC-LM-09-02) and the
    // first-login password change (TC-LM-06-04 / 07-04) can be tried too.
    // =================================================================
    [HttpGet, LabManagerPublic]
    public IActionResult DevSignIn() => DevSignInAllowed ? DevSignInPage(null, null) : NotFound();

    [HttpPost, LabManagerPublic]
    public async Task<IActionResult> DevSignIn(string? email, string? password)
    {
        if (!DevSignInAllowed) return NotFound();

        var result = await _login.CheckCredentialsAsync(email, password);
        if (!result.Success) return DevSignInPage(result.Error, email);

        _currentUser.SignIn(result.UserId, result.Role);

        if (result.LabManagerId is not null)
            return result.MustChangePassword ? RedirectToAction(nameof(ChangePassword)) : RedirectToAction(nameof(Dashboard));

        string note = result.MustChangePassword
            ? $"Signed in as a {result.Role} who is still on a temporary password ([User].IsFirstLogin = 1). The group's login must now send them " +
              "to change their password before granting access (AC-06-03 / AC-07-05). LoginCheckResult.MustChangePassword = true."
            : $"Signed in as a {result.Role}.";
        return DevSignInPage(null, email, note + " Now open /LabManager/Dashboard to check that access is denied (TC-LM-09-06).");
    }

    private bool DevSignInAllowed => _env.IsDevelopment() && _options.Auth.EnableDevSignIn;

    private ContentResult DevSignInPage(string? error, string? email, string? info = null)
    {
        var tokens = HttpContext.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(HttpContext);
        string E(string? s) => WebUtility.HtmlEncode(s ?? "");

        string html = $@"<!DOCTYPE html><html><head><meta charset=""utf-8""><title>Lab Manager - dev sign in</title>
<style>body{{font-family:Segoe UI,Arial,sans-serif;background:#f1f5f9;display:flex;justify-content:center;padding-top:60px}}
form{{background:#fff;padding:28px;border-radius:10px;width:380px;box-shadow:0 2px 10px #0001}}
input{{width:100%;padding:9px;margin:6px 0 14px;box-sizing:border-box}}button{{padding:10px 18px;background:#1d3a6b;color:#fff;border:0;border-radius:6px}}
.err{{color:#b91c1c}}.info{{color:#166534}}small{{color:#64748b}}</style></head><body>
<form method=""post"" action=""{E(Url.Action(nameof(DevSignIn)))}"">
<h3>Development sign-in</h3>
<small>Only available while running in Development. Use the group's login page in the real project.</small>
{(error is null ? "" : $"<p class=\"err\">{E(error)}</p>")}
{(info is null ? "" : $"<p class=\"info\">{E(info)}</p>")}
<input type=""hidden"" name=""{E(tokens.FormFieldName)}"" value=""{E(tokens.RequestToken)}"" />
<label>Email</label><input name=""email"" type=""email"" value=""{E(email)}"" required />
<label>Password</label><input name=""password"" type=""password"" required />
<button type=""submit"">Sign in</button>
</form></body></html>";

        return Content(html, "text/html");
    }
}
