namespace expense_management_app.Contracts.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public AuthUserResponse User { get; set; } = null!;
}