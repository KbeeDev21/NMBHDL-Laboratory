
using System.Net;
using System.Net.Mail;
using NMBHDL.LabTechnician.Configuration;

namespace NMBHDL.LabTechnician.Services;

public sealed class LabTechEmailService
{
    private readonly LabTechOptions _options;

    public LabTechEmailService(LabTechOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<(bool Success, string Message)> SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail) ||
            !MailAddress.TryCreate(toEmail.Trim(), out var recipient))
        {
            return (false, "A valid recipient email address is required.");
        }

        if (string.IsNullOrWhiteSpace(subject))
            return (false, "An email subject is required.");

        if (string.IsNullOrWhiteSpace(htmlBody))
            return (false, "An email body is required.");

        var emailOptions = _options.Email;
        var mode = emailOptions.Mode?.Trim();

        if (string.Equals(mode, "LogOnly", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                $"[LabTech email - LogOnly] To: {recipient.Address}; Subject: {subject}");

            return (true, "Email logged only; no message was delivered.");
        }

        if (string.IsNullOrWhiteSpace(emailOptions.SenderEmail) ||
            !MailAddress.TryCreate(emailOptions.SenderEmail.Trim(), out var sender))
        {
            return (false, "Configure a valid Lab Technician sender email address.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(
                sender.Address,
                emailOptions.SenderName ?? string.Empty),
            Subject = subject.Trim(),
            Body = htmlBody,
            IsBodyHtml = true
        };

        message.To.Add(recipient);

        try
        {
            if (string.Equals(mode, "Pickup", StringComparison.OrdinalIgnoreCase))
            {
                var pickupDirectory = emailOptions.PickupDirectory;

                if (string.IsNullOrWhiteSpace(pickupDirectory))
                    return (false, "Configure a local email pickup directory.");

                if (!Path.IsPathRooted(pickupDirectory))
                {
                    return (false,
                        "The pickup directory must be an absolute path for this service.");
                }

                Directory.CreateDirectory(pickupDirectory);

                using var client = new SmtpClient
                {
                    DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                    PickupDirectoryLocation = pickupDirectory
                };

                await client.SendMailAsync(message, cancellationToken);

                return (true, "Email saved to the local pickup directory.");
            }

            if (string.Equals(mode, "Smtp", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(emailOptions.SmtpHost))
                    return (false, "Configure an SMTP host before sending email.");

                if (emailOptions.SmtpPort is < 1 or > 65535)
                    return (false, "Configure a valid SMTP port.");

                using var client = new SmtpClient(
                    emailOptions.SmtpHost,
                    emailOptions.SmtpPort)
                {
                    EnableSsl = emailOptions.EnableSsl
                };

                if (!string.IsNullOrWhiteSpace(emailOptions.Username))
                {
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(
                        emailOptions.Username,
                        emailOptions.Password);
                }

                await client.SendMailAsync(message, cancellationToken);

                return (true, "Email sent successfully.");
            }

            return (false,
                "Unsupported email mode. Use LogOnly, Pickup, or Smtp.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Lab Technician email delivery failed: {ex}");
            return (false, "Email delivery failed. Check the application logs.");
        }
    }
}
