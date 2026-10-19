#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace NMBHLabSys.LabManagerSubsystem.Services;

public interface ILabManagerPasswordService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string storedHash);

    /// <summary>A random password that always meets the complexity rules (AC-06-02 / AC-07-02).</summary>
    string GenerateTemporaryPassword(int length = 12);

    /// <summary>A random, URL-safe token for the reset link, plus the hash to store (AC-09-03).</summary>
    (string Token, string TokenHash) GenerateResetToken();
    string HashResetToken(string token);
}

/// <summary>
/// Passwords are hashed with ASP.NET Core's built-in PasswordHasher (PBKDF2 with a random salt),
/// so no extra NuGet package is needed.
///
/// IMPORTANT FOR THE GROUP: the doctors and technicians this subsystem creates log in through
/// the group's login page, so that login must check passwords the same way. Either the login
/// calls ILabManagerLoginService.CheckCredentialsAsync (easiest), or, if the group already uses
/// another hasher (e.g. BCrypt), change ONLY HashPassword and VerifyPassword in this class.
/// </summary>
public class LabManagerPasswordService : ILabManagerPasswordService
{
    // PasswordHasher needs a "user" type; it doesn't use it for anything.
    private sealed class HashUser { }

    private static readonly PasswordHasher<HashUser> Hasher = new();
    private static readonly HashUser User = new();

    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";   // no I or O (look like 1 and 0)
    private const string Lower = "abcdefghjkmnpqrstuvwxyz";    // no l
    private const string Digits = "23456789";                  // no 0 or 1
    private const string Special = "@#$!%&*?";

    public string HashPassword(string password) => Hasher.HashPassword(User, password);

    public bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;
        try
        {
            var result = Hasher.VerifyHashedPassword(User, storedHash, password);
            return result != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // A hash in another format (or garbage) means "wrong password", not a crash.
            return false;
        }
    }

    public string GenerateTemporaryPassword(int length = 12)
    {
        if (length < 8) length = 8;
        string all = Upper + Lower + Digits + Special;

        var chars = new char[length];
        chars[0] = Pick(Upper);
        chars[1] = Pick(Lower);
        chars[2] = Pick(Digits);
        chars[3] = Pick(Special);
        for (int i = 4; i < length; i++) chars[i] = Pick(all);

        // Shuffle so the required characters aren't always at the front
        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }

    public (string Token, string TokenHash) GenerateResetToken()
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return (token, HashResetToken(token));
    }

    // Only the hash is stored, so someone who can read the database can't use a pending link.
    public string HashResetToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    // RandomNumberGenerator, not System.Random: these passwords are emailed out, so they must not be guessable.
    private static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
}
