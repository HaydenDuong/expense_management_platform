using System.ComponentModel.DataAnnotations;

namespace expense_management_app.Contracts.Auth;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}