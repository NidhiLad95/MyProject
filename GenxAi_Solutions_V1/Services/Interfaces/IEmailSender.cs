namespace GenxAi_Solutions_V1.Services.Interfaces
{
    public interface IEmailSender
    {
        Task SendAsync(string toEmail, string subject, string htmlBody);
    }
}
