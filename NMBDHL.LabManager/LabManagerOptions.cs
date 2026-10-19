#nullable enable
namespace NMBHLabSys.LabManagerSubsystem;

/// <summary>
/// Every setting the Lab Manager subsystem reads. They all live under one
/// "LabManager" section in appsettings.json, so they can never collide with
/// settings that belong to the Doctor or Lab Technician subsystems.
/// Anything you leave out of appsettings.json keeps the default shown here.
/// </summary>
public class LabManagerOptions
{
    public const string SectionName = "LabManager";

    /// <summary>
    /// Name of the connection string to use. If it isn't found,
    /// "DefaultConnection" is used instead (the group's shared database).
    /// </summary>
    public string ConnectionStringName { get; set; } = "LabManagerConnection";

    /// <summary>Printed on the management report and in emails (AC-08-04).</summary>
    public string LaboratoryName { get; set; } = "Nelson Mandela Bay Haematology Diagnostic Laboratory";

    /// <summary>Order numbers look like ORD-2026-001 (AC-05-02).</summary>
    public string OrderNumberPrefix { get; set; } = "ORD";

    public LabManagerAuthOptions Auth { get; set; } = new();
    public LabManagerReportOptions Report { get; set; } = new();
    public LabManagerEmailOptions Email { get; set; } = new();
}

/// <summary>
/// How the subsystem finds out who is logged in. The group's login page owns
/// the sign-in, so these must match whatever that login stores.
/// Both the session and claims (cookie authentication) are checked.
/// </summary>
public class LabManagerAuthOptions
{
    /// <summary>Where users are sent when nobody is logged in (AC-09-07).</summary>
    public string LoginPath { get; set; } = "/Home/LoginPage";

    /// <summary>Session key the group's login uses for the logged-in user's [User].UserID.</summary>
    public string SessionUserIdKey { get; set; } = "UserID";

    /// <summary>Session key the group's login uses for the user's role.</summary>
    public string SessionRoleKey { get; set; } = "Role";

    /// <summary>
    /// Role names the backend reports (LoginCheckResult.Role). grp-04-14 has no Role column, so the role
    /// comes from the table the user is in: LabManager -> ManagerRole, Doctor -> DoctorRole,
    /// LabTechnician -> TechnicianRole. Access to manager pages is decided by the LabManager table itself.
    /// </summary>
    public string ManagerRole { get; set; } = "Manager";
    public string DoctorRole { get; set; } = "Doctor";
    public string TechnicianRole { get; set; } = "Technician";

    /// <summary>Forgot-password links stop working after this many minutes.</summary>
    public int ResetTokenLifetimeMinutes { get; set; } = 60;

    /// <summary>
    /// Allows /LabManager/DevSignIn so you can test locally before the group's
    /// login page is merged in. It ONLY works when the app runs in Development.
    /// </summary>
    public bool EnableDevSignIn { get; set; } = true;
}

public class LabManagerReportOptions
{
    /// <summary>
    /// Which TestResult.Status values count as a performed test on the management report.
    /// Empty (the default) = every TestResult with a CompletionDateTime in the date range counts.
    /// Example once the Lab Technician subsystem has settled its statuses: [ "Completed", "Verified" ]
    /// </summary>
    public string[] CountedResultStatuses { get; set; } = System.Array.Empty<string>();
}

public class LabManagerEmailOptions
{
    /// <summary>
    /// "Pickup": every email is saved as an .eml file in PickupDirectory (open it with Outlook/Mail).
    ///           Best for local testing: you can read temporary passwords and reset links.
    /// "Smtp":   really sends the email through SmtpHost.
    /// "LogOnly": writes the email to the console log only.
    /// </summary>
    public string Mode { get; set; } = "Pickup";

    public string PickupDirectory { get; set; } = "LabManagerMail";

    public string SenderName { get; set; } = "NMB-HLabSys";
    public string SenderEmail { get; set; } = "nmbhlabsys@gmail.com";

    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string SmtpUsername { get; set; } = "";

    /// <summary>Never commit this. Use: dotnet user-secrets set "LabManager:Email:SmtpPassword" "..."</summary>
    public string SmtpPassword { get; set; } = "";
}
