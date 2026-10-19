#nullable enable
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace NMBHLabSys.LabManagerSubsystem.Services;

/// <summary>
/// Answers "who is logged in?" for the Lab Manager pages.
/// The login page belongs to the group, so this reads what that login leaves behind:
///   1. the session (keys from LabManager:Auth:SessionUserIdKey / SessionRoleKey), or
///   2. the signed-in user's claims, if the group uses cookie authentication.
/// </summary>
public interface ILabManagerCurrentUser
{
    int? UserId { get; }
    string? Role { get; }

    /// <summary>Stores the user in the session (used by DevSignIn and by the group's login if it wants to).</summary>
    void SignIn(int userId, string role);
    void SignOut();
}

public class LabManagerCurrentUser : ILabManagerCurrentUser
{
    private readonly IHttpContextAccessor _http;
    private readonly LabManagerAuthOptions _auth;

    public LabManagerCurrentUser(IHttpContextAccessor http, IOptions<LabManagerOptions> options)
    {
        _http = http;
        _auth = options.Value.Auth;
    }

    private HttpContext? Context => _http.HttpContext;

    /// <summary>The session, or null when the app hasn't called AddSession/UseSession.</summary>
    private ISession? Session
    {
        get
        {
            var context = Context;
            if (context is null) return null;
            try
            {
                var session = context.Features.Get<Microsoft.AspNetCore.Http.Features.ISessionFeature>()?.Session;
                return session is { IsAvailable: true } ? session : null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }

    public int? UserId
    {
        get
        {
            var session = Session;
            if (session is not null)
            {
                // Works whether the login stored it with SetString("UserID", "5") or SetInt32("UserID", 5)
                string? text = session.GetString(_auth.SessionUserIdKey);
                if (int.TryParse(text, out int fromString)) return fromString;

                int? fromInt = session.GetInt32(_auth.SessionUserIdKey);
                if (fromInt.HasValue) return fromInt;
            }

            var user = Context?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                string? claim = user.FindFirstValue(ClaimTypes.NameIdentifier)
                                ?? user.FindFirstValue(_auth.SessionUserIdKey)
                                ?? user.FindFirstValue("sub");
                if (int.TryParse(claim, out int fromClaim)) return fromClaim;
            }

            return null;
        }
    }

    public string? Role
    {
        get
        {
            string? role = Session?.GetString(_auth.SessionRoleKey);
            if (!string.IsNullOrWhiteSpace(role)) return role;

            var user = Context?.User;
            if (user?.Identity?.IsAuthenticated == true)
                return user.FindFirstValue(ClaimTypes.Role) ?? user.FindFirstValue(_auth.SessionRoleKey);

            return null;
        }
    }

    public void SignIn(int userId, string role)
    {
        var session = Session ?? throw new InvalidOperationException(
            "Session is not enabled. Add builder.Services.AddSession() and app.UseSession() to Program.cs.");
        session.SetString(_auth.SessionUserIdKey, userId.ToString());
        session.SetString(_auth.SessionRoleKey, role);
    }

    public void SignOut() => Session?.Clear();
}
