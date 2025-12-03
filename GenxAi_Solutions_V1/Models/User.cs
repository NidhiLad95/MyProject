using System.ComponentModel.DataAnnotations;

namespace GenxAi_Solutions_V1.Models
{
    public class User
    {
        public int Id { get; set; }
        //public int CompanyId { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }
        //public string PasswordSalt { get; set; }
    }

    public class LoginViewModel
    {
        public string Email { get; set; }
        public string Password { get; set; }
        
    }
}

  
    public class ForgotPasswordViewModel
    {
        public string Email { get; set; }
    }

    public class ResetPasswordViewModel
    {
        public string Email { get; set; }
        public string Token { get; set; }
        public string NewPassword { get; set; }
    }

    public class SmtpOptions
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public bool UseStartTls { get; set; } = true;
        public string FromName { get; set; } = "GenXAI";
        public string FromEmail { get; set; } = "";
        public string User { get; set; } = "";
        public string Pass { get; set; } = "";
    }

public class ChangePasswordViewModel
{
    public int Id {  get; set; }
    
    public string OldPassword { get; set; }

   
    public string NewPassword { get; set; }

    
    public string ConfirmPassword { get; set; }
}




