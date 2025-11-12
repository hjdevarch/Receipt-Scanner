namespace ReceiptScanner.Application.Interfaces;

public interface IEmailService
{
    Task<bool> SendEmailAsync(string toEmail, string subject, string body);
    Task<bool> SendActivationEmailAsync(string toEmail, string userName, string activationLink);
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink);
}
