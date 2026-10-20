using System.ComponentModel.DataAnnotations;

namespace NMBDHL.Auth.Model
{
    public class Login
    {
        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your password.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberLogin { get; set; } = true;

        public string? ReturnUrl { get; set; }

        public string? Role { get; set; }
    }
}