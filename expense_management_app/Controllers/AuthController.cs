using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity; // Password Hasher Dependecy
using Microsoft.Extensions.Options;
using expense_management_app.Contracts.Auth;
using expense_management_app.Models.Identity;
using expense_management_app.Infrastructure.Persistence;
using expense_management_app.Services;
using expense_management_app.Options;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace expense_management_app.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    // Dependencies
    private readonly AppDbContext _context;
    private readonly PasswordHasher<AppUser> _passwordHasher;
    private readonly ILogger<AuthController> _logger;
    private readonly JwtOptions _jwtOptions;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;

    // Constructor
    public AuthController(
        AppDbContext context, 
        PasswordHasher<AppUser> passwordHasher,
        ILogger<AuthController> logger,
        IOptions<JwtOptions> jwtOptions,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
        _jwtOptions = jwtOptions.Value;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
    }

    // Registeration Method
    [HttpPost("register")]
    public async Task<ActionResult<AuthUserResponse>> Register(RegisterRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        _logger.LogInformation("Registration attempt received");

        var emailExists = await _context.AppUsers
            // "AnyAsync" asked the database:
            // Is there at least one matching row?
            // It returns: True / False
            .AnyAsync(user => user.NormalizedEmail == normalizedEmail);
        
        if (emailExists)
        {
            _logger.LogWarning("Registration rejected because this email already exists");
            return Conflict();
        }

        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = string.Empty,
            CreatedAt = now,
            UpdatedAt = now
        };

        user.PasswordHash = _passwordHasher.HashPassword(
            user, 
            request.Password);

        _context.AppUsers.Add(user);
        await _context.SaveChangesAsync();
        _logger.LogInformation("User registered successfully with id {UserId}", user.Id);
        
        var response = new AuthUserResponse
        {
            Id = user.Id,
            Email = user.Email,
            CreatedAt = user.CreatedAt
        };

        return StatusCode(StatusCodes.Status201Created, response);
    }

    // Login Method
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        _logger.LogInformation("Login attempt received");

        var user = await _context.AppUsers
            // "FirstOrDefaultAsync" returns:
            // AppUser? user
            // Either: matching user object or null
            // => Return the actual record
            .FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail);
        
        if (user is null)
        {
            _logger.LogWarning("Login failed due to invalid credentials.");
            return Unauthorized();
        }

        // "VerifyHashedPassword" can return: Failed, Success, and SuccessRehashNeeded
        var result = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);
        
        if (result == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Login failed due to invalid credentials.");
            return Unauthorized();
        }

        // Create AccessToken after login successed
        var accessToken = _jwtTokenService.GenerateAccessToken(user);

        // Create RefreshToken after login successed
        // Generate a random 64 bytes string & convert it into a hashed refresh token
        var rawRefreshToken = _refreshTokenService.GenerateRefreshToken();
        var hashedRefreshToken = _refreshTokenService.HashRefreshToken(rawRefreshToken);

        var now = DateTime.UtcNow;
        var refreshToken = new RefreshToken
        {
            AppUserId = user.Id,
            TokenHash = hashedRefreshToken,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays)
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        var response = new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            User = new AuthUserResponse
            {
                Id = user.Id,
                Email = user.Email,
                CreatedAt = user.CreatedAt
            }
        };

        _logger.LogInformation("Login succeeded for user id {UserId}", user.Id);

        // Or return Ok(response);
        return StatusCode(StatusCodes.Status200OK, response); 
    }
    
    // RefreshToken Rotation Http Method
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
    {
        // Receive raw refresh token from Client's request
        // & Hashed with IRefreshTokenService method
        var hashedRefreshToken = _refreshTokenService.HashRefreshToken(request.RefreshToken);

        // Query for the matching row from RefreshTokens table
        // Also, includes related AppUser info
        var storedRefreshToken = await _context.RefreshTokens
            
            // This will include the corresponding AppUser object
            // throught the stored value in "AppUserId" column of the matching row
            .Include(token => token.AppUser)
            .FirstOrDefaultAsync(token => token.TokenHash == hashedRefreshToken);
        
        // Validity Checks
        // 1. Reject if not found
        if (storedRefreshToken is null)
        {
            _logger.LogWarning("Refresh token rejected because it was not found.");
            return Unauthorized();
        }

        // 2. Reject if this token is expired,
        // or this stored token is not null (null by default upon refresh token creation during login)
        // Changed from null to a DateTime value after Http POST /auth/refresh
        // So: RevokedAt = null = this token is still active
        //     RevokedAt has a DateTime => already used / killed
        var now = DateTime.UtcNow;

        if (storedRefreshToken.ExpiresAt <= now || storedRefreshToken.RevokedAt is not null)
        {
            _logger.LogWarning("Refresh token rejected because it expired or already revoked for user id {UserId}", storedRefreshToken.AppUserId);
            return Unauthorized();
        }

        // Revoke this token 
        storedRefreshToken.RevokedAt = now;

        // Generate a new AccessToken
        var accessToken = _jwtTokenService.GenerateAccessToken(storedRefreshToken.AppUser);

        // Generate a new RefreshToken
        var rawRefreshToken = _refreshTokenService.GenerateRefreshToken();
        var hashRefreshToken = _refreshTokenService.HashRefreshToken(rawRefreshToken);

        // Store the hashed version of the new refresh token into the RefreshTokens table
        var refreshToken = new RefreshToken
        {
            AppUserId = storedRefreshToken.AppUserId,
            TokenHash = hashRefreshToken,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays)
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Refresh token rotation succeeded for user id {UserId}", storedRefreshToken.AppUserId);

        // Return AuthResponse
        var response = new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            User = new AuthUserResponse
            {
                Id = storedRefreshToken.AppUser.Id,
                Email = storedRefreshToken.AppUser.Email,
                CreatedAt = storedRefreshToken.AppUser.CreatedAt
            }
        };

        return Ok(response);
    }

    // Logout HTTP Method - Revoke the current RefreshToken
    // No new refresh token is issued. The user must log in again to start a new session.
    [HttpPost("logout")]
    public async Task<ActionResult> Logout(LogoutRequest request)
    {
        var hashedRefreshToken = _refreshTokenService.HashRefreshToken(request.RefreshToken);

        var storedRefreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == hashedRefreshToken);
        
        if (storedRefreshToken is null)
        {
            _logger.LogWarning("Refresh Token is rejected because it was not found.");
            return Unauthorized();
        }

        var now = DateTime.UtcNow;

        if (storedRefreshToken.ExpiresAt <= now || storedRefreshToken.RevokedAt is not null)
        {
            _logger.LogWarning("Logout rejected because it is expired or already revoked.");
            return Unauthorized();
        }

        storedRefreshToken.RevokedAt = now;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Refresh token is revoked for this user id {UserId}", storedRefreshToken.AppUserId);

        return NoContent();
    }

    // Authorization HTTP endpoint
    // ControllerBase gives AuthController access to the following HTTP request properties:
    // User, Request, Response, HttpContext, ModelState
    // This endpoint does not take a request DTO.
    // The caller's identity comes from the Authorization header:
    //
    // Authorization: Bearer <accessToken>
    //
    // ASP.NET Core JWT middleware validates the token before this action runs.
    // If the token is valid, it creates HttpContext.User from the JWT claims.
    // Because AuthController inherits from ControllerBase, we can access HttpContext.User as User.
    // The "sub" claim contains the AppUser.Id that was written when the access token was generated.
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthUserResponse>> Me()
    {
        // Because the current code of JWT Service created claim as:
        // new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString())
        // [Cont.] from above: User being used here is shorthanded for HttpContext.User - a ClaimsPrincipal
        // A "ClaimsPrincipal" represents the authenticated caller
        // The flow is:
        // 1. Client sends Authorization: Bearer <access token>
        // 2. JWT middleware validates token
        // 3. Middleware creates a ClaimsPrincipal from the token claims
        // 4. Middleware sets HttpContext.User ~ JWT creates this latter and it contains claims & it exists only for this request
        // 5. Controller action reads "HttpContext.User" (which is User)
        // so the following code means: Look at the authenticated caller's claims and find the claim named "sub"
        var userIdValue = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        // Validity Check
        if (!int.TryParse(userIdValue, out var userId))
        {
            _logger.LogWarning("Authenticated request rejected because the subject claim was missing or invalid.");
            return Unauthorized();
        }

        // Find this user from the DB, instead of only trusting the token
        // Because this user might have been deleted / disabled / changed.
        // For a production-shaped app, it's reasonable for "/auth/me" to fetch the current user data.
        var user = await _context.AppUsers.FindAsync(userId);

        if (user is null)
        {
            _logger.LogWarning("Authenticated request rejected because user id {UserId} was not found.", userId);
            return Unauthorized();
        }

        var response = new AuthUserResponse
        {
            Id = user.Id,
            Email = user.Email,
            CreatedAt = user.CreatedAt
        };

        _logger.LogWarning("Current user profile returned for user id {UserId}.", user.Id);
        return Ok(response);
    }
}