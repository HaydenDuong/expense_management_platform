using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity; // Password Hasher Dependecy
using expense_management_app.Contracts.Auth;
using expense_management_app.Models;
using expense_management_app.Infrastructure.Persistence;

namespace expense_management_app.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    // Dependecies
    private readonly AppDbContext _context;
    private readonly PasswordHasher<AppUser> _passwordHasher;
    private readonly ILogger<AuthController> _logger;

    // Constructor
    public AuthController(
        AppDbContext context, 
        PasswordHasher<AppUser> passwordHasher,
        ILogger<AuthController> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    // Registeration Method
    [HttpPost("register")]
    public async Task<ActionResult<AuthUserResponse>> Register(RegisterRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        _logger.LogInformation("Registration attempt received");

        var emailExists = await _context.AppUsers
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

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

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
}