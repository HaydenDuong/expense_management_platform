using expense_management_app.Models;

namespace expense_management_app.Services;

// This is an interface which should epress what the app needs, not how Jwt works internally
// Controller does not need to know signing keys.
// Controller does not need to know claims construction.
// Controller only says: given this user (AppUser user), make me an access token for this object.
public interface IJwtTokenService
{
    string GenerateAccessToken(AppUser user);
}