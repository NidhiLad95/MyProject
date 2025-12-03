// Services/SmtpEmailSender.cs
using GenxAi_Solutions.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _opt;
    public SmtpEmailSender(IOptions<SmtpOptions> opt) => _opt = opt.Value;

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        using var client = new SmtpClient(_opt.Host, _opt.Port)
        {
            EnableSsl = _opt.UseStartTls,
            Credentials = new NetworkCredential(_opt.User, _opt.Pass)
        };

        var msg = new MailMessage
        {
            From = new MailAddress(_opt.FromEmail, _opt.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        msg.To.Add(toEmail);

        await client.SendMailAsync(msg);
    }
}
