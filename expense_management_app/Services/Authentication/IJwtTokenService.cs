using expense_management_app.Models.Identity;

namespace expense_management_app.Services.Authentication;

public interface IJwtTokenService
{
    string GenerateAccessToken(AppUser user);
}