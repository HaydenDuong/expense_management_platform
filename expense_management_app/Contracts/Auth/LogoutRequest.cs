using System.ComponentModel.DataAnnotations;

namespace expense_management_app.Contracts.Auth;

public class LogoutRequest
{
    [Required]
    [MinLength(20)]
    public string RefreshToken { get; set; } = string.Empty;
}