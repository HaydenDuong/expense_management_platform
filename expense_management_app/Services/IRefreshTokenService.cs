namespace expense_management_app.Services;

public interface IRefreshTokenService
{
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
}