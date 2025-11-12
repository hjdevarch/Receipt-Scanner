using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReceiptScanner.Application.Interfaces;
using ReceiptScanner.Application.Settings;

namespace ReceiptScanner.Application.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
    {
        try
        {
            using var smtpClient = new SmtpClient(_emailSettings.SmtpHost, _emailSettings.SmtpPort)
            {
                Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password),
                EnableSsl = _emailSettings.EnableSsl
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            await smtpClient.SendMailAsync(mailMessage);
            _logger.LogInformation("Email sent successfully to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendActivationEmailAsync(string toEmail, string userName, string activationLink)
    {
        var subject = "Activate Your Receipt Scanner Account";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <h2>Welcome to Receipt Scanner, {userName}!</h2>
                <p>Thank you for registering with Receipt Scanner. To complete your registration, please activate your account by clicking the link below:</p>
                <p style='margin: 20px 0;'>
                    <a href='{activationLink}' style='background-color: #4CAF50; color: white; padding: 14px 20px; text-decoration: none; border-radius: 4px; display: inline-block;'>
                        Activate Account
                    </a>
                </p>
                <p>Or copy and paste this link into your browser:</p>
                <p style='color: #666;'>{activationLink}</p>
                <p>This activation link will expire in 24 hours.</p>
                <p>If you didn't create an account, please ignore this email.</p>
                <hr style='margin: 20px 0; border: none; border-top: 1px solid #ddd;'>
                <p style='color: #666; font-size: 12px;'>Receipt Scanner Team</p>
            </body>
            </html>
        ";

        return await SendEmailAsync(toEmail, subject, body);
    }
}
