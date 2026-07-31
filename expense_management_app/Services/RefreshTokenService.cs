using System.Security.Cryptography;

namespace expense_management_app.Services;

public class RefreshTokenService : IRefreshTokenService
{   
    // 64 bytes = 512 bits of randomness => very hard to guess while easy to send as a string
    // Why Base64?
    // Raw bytes are not convenient in JSON => Base64 converts bytes to a text string
    public string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);

        return Convert.ToBase64String(randomBytes);
    }

    public string HashRefreshToken(string refreshToken)
    {
        var tokenBytes = System.Text.Encoding.UTF8.GetBytes(refreshToken);
        var hashBytes = SHA256.HashData(tokenBytes);

        return Convert.ToBase64String(hashBytes);
    }
}