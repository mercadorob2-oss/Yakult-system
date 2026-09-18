using System.ComponentModel.DataAnnotations;

namespace Inventory.RequestPortal.Models.ViewModels
{
    /// <summary>
    /// ViewModel for Login form.
    /// TRANSLATED FROM: Yakult.Inventory.App/Pages/LoginPage.cs (form fields)
    /// </summary>
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Please enter your username.")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }
}
