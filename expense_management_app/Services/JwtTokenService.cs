using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using expense_management_app.Models;
using expense_management_app.Options;

namespace expense_management_app.Services;

// No Logging is needed, because:
// Exception says what is wrong
// Startup / request logs will capture the failure.
// Do not want to risk logging the "Secret" itself
public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _jwtOptions;

    // Guardrails for validating config could be included in constructor
    public JwtTokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;

        // Check for valid "Issuer"
        if (string.IsNullOrWhiteSpace(_jwtOptions.Issuer))
        {
            throw new InvalidOperationException("Jwt issuer is not configured.");
        }

        // Check for valid "Audience"
        if (string.IsNullOrWhiteSpace(_jwtOptions.Audience))
        {
            throw new InvalidOperationException("Jwt audience is not configured.");
        }

        // Check for valid "Secret"
        if (string.IsNullOrWhiteSpace(_jwtOptions.Secret))
        {
            throw new InvalidOperationException("Jwt secret is not configured.");
        }

        // Length check for the "Secret"
        if (Encoding.UTF8.GetByteCount(_jwtOptions.Secret) < 32)
        {
            throw new InvalidOperationException("Jwt secret must at least 32 bytes.");
        }

        // Check if "AccessTokenMinutes" is a positive value or not
        if (_jwtOptions.AccessTokenMinutes <= 0)
        {
            throw new InvalidOperationException("Jwt access token lifetime must be greater than zero.");
        }

        // Check if "RefreshTokenDays" is a positive value or not
        if (_jwtOptions.RefreshTokenDays <= 0)
        {
            throw new InvalidOperationException("Jwt refresh token lifetime must greater than zero.");
        }
    }

    public string GenerateAccessToken(AppUser user)
    {
        // Build claims
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Build signing key
        // This turn the "Secret" (in "appsetting.Development.json/Jwt/Secret")
        // into a cryptographic key
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtOptions.Secret)
        );

        // Sign the token using HMAC SHA-256
        // Because this is not password hashing, so HMAC SHA-256 is fine here
        // For JWT signing, HMAC SHA-256 is common
        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256
        );

        // Build token
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes),
            signingCredentials: credentials
        );

        // Build token string
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}