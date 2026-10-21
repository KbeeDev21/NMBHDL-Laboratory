
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NMBHDL.LabTechnician.Configuration;
using NMBHDL.LabTechnician.Services;

namespace NMBHDL.LabTechnician;

/// <summary>
/// Registers services owned by the Lab Technician subsystem.
/// </summary>
public static class LabTechnicianServiceCollectionExtensions
{
    public static IServiceCollection AddLabTechnicianSubsystem(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new LabTechOptions();

        options.LaboratoryName =
            configuration[$"{LabTechOptions.SectionName}:LaboratoryName"]
            ?? options.LaboratoryName;

        options.Email.Mode =
            configuration[$"{LabTechOptions.SectionName}:Email:Mode"]
            ?? options.Email.Mode;

        options.Email.PickupDirectory =
            configuration[$"{LabTechOptions.SectionName}:Email:PickupDirectory"]
            ?? options.Email.PickupDirectory;

        options.Email.SenderName =
            configuration[$"{LabTechOptions.SectionName}:Email:SenderName"]
            ?? options.Email.SenderName;

        options.Email.SenderEmail =
            configuration[$"{LabTechOptions.SectionName}:Email:SenderEmail"]
            ?? options.Email.SenderEmail;

        options.Email.SmtpHost =
            configuration[$"{LabTechOptions.SectionName}:Email:SmtpHost"]
            ?? options.Email.SmtpHost;

        options.Email.SmtpPort =
            int.TryParse(
                configuration[$"{LabTechOptions.SectionName}:Email:SmtpPort"],
                out var smtpPort)
                ? smtpPort
                : options.Email.SmtpPort;

        options.Email.EnableSsl =
            bool.TryParse(
                configuration[$"{LabTechOptions.SectionName}:Email:EnableSsl"],
                out var enableSsl)
                ? enableSsl
                : options.Email.EnableSsl;

        options.Email.Username =
            configuration[$"{LabTechOptions.SectionName}:Email:Username"]
            ?? options.Email.Username;

        options.Email.Password =
            configuration[$"{LabTechOptions.SectionName}:Email:Password"]
            ?? options.Email.Password;

        services.AddSingleton(options);
        services.AddScoped<LabTechEmailService>();

        return services;
    }
}
