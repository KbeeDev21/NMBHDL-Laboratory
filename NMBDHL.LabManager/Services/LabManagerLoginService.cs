#nullable enable
using System.Threading.Tasks;
using NMBHLabSys.LabManagerSubsystem.Data;

namespace NMBHLabSys.LabManagerSubsystem.Services;

public class LoginCheckResult
{
    public const string InvalidCredentialsMessage = Rules.LabManagerMessages.InvalidCredentials;

    public bool Success { get; init; }
    public int UserId { get; init; }
    public string Role { get; init; } = "";

    /// <summary>
    /// AC-06-03 / AC-07-05 / AC-09: true for an account still on its temporary password.
    /// The login page must then send the user to a change-password page before anything else.
    /// </summary>
    public bool MustChangePassword { get; init; }

    /// <summary>Set for laboratory managers ([LabManager].LabManagerID).</summary>
    public int? LabManagerId { get; init; }

    public string? Error { get; init; }
}

/// <summary>
/// Checks an email + password against the shared [User] table (column PasswaordHash), using the same hashing the
/// Lab Manager uses when it creates doctor and technician accounts.
///
/// The group's login page can call this so that everyone (managers, doctors, technicians)
/// is checked the same way:
///
///     var result = await _labManagerLogin.CheckCredentialsAsync(email, password);
///     if (!result.Success) { ModelState.AddModelError("", result.Error!); return View(); }
///     HttpContext.Session.SetString("UserID", result.UserId.ToString());
///     HttpContext.Session.SetString("Role", result.Role);
///     if (result.MustChangePassword) return RedirectToAction("ChangePassword", ...);   // AC-06-03 / AC-07-05
/// </summary>
public interface ILabManagerLoginService
{
    Task<LoginCheckResult> CheckCredentialsAsync(string? email, string? password);
}

public class LabManagerLoginService : ILabManagerLoginService
{
    private readonly ILabManagerData _data;
    private readonly ILabManagerPasswordService _passwords;

    public LabManagerLoginService(ILabManagerData data, ILabManagerPasswordService passwords)
    {
        _data = data;
        _passwords = passwords;
    }

    public async Task<LoginCheckResult> CheckCredentialsAsync(string? email, string? password)
    {
        // AC-09-02: the same message whether the email or the password is wrong,
        // so the page doesn't reveal which emails are registered.
        var fail = new LoginCheckResult { Success = false, Error = LoginCheckResult.InvalidCredentialsMessage };

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return fail;

        var user = await _data.Accounts.GetUserByEmailAsync(email.Trim());
        if (user is null || !_passwords.VerifyPassword(password, user.PasswordHash)) return fail;

        // [User].IsActive = 0: the account was deactivated by the manager
        if (!user.IsActive)
            return new LoginCheckResult { Success = false, Error = "This account has been deactivated. Contact the laboratory manager." };

        return new LoginCheckResult
        {
            Success = true,
            UserId = user.UserId,
            Role = user.Role,                       // worked out from the LabManager / Doctor / LabTechnician tables
            MustChangePassword = user.MustChangePassword,   // [User].IsFirstLogin
            LabManagerId = user.LabManagerId
        };
    }
}
