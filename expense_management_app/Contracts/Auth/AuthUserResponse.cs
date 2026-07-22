namespace expense_management_app.Contracts.Auth;

public class AuthUserResponse
{
    public int Id { get; set;}
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}