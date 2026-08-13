using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using expense_management_app.Contracts.Expenses;
using expense_management_app.Contracts.Expenses.Responses;
using expense_management_app.Infrastructure.Persistence;
using expense_management_app.Models.Expenses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace expense_management_app.Controllers;

[ApiController]
[Authorize]
[Route("tags")]
public class TagsController : ControllerBase
{
    // Dependencies
    private readonly AppDbContext _context;
    private readonly ILogger<TagsController> _logger;

    // Constructor
    public TagsController(
        AppDbContext context,
        ILogger<TagsController> logger
    )
    {   
        // Field = injected parameter
        _context = context;
        _logger = logger;
    }

    // Methods
    // Allow the current user to create a tag
    [HttpPost]
    public async Task<ActionResult<TagResponse>> CreateTag(
        [FromBody] CreateTagRequest request
    )
    {
        // UserId Validity Check
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim was missing or invalid.");
            return Unauthorized();
        }

        // Validity Check for input tag name
        var normalizedName = request.Name.Trim().ToUpperInvariant();

        var tagNameExist = await _context.Tags
            .AnyAsync(tag =>
                tag.AppUserId == userId &&
                tag.Name == normalizedName);

        if (tagNameExist)
        {
            _logger.LogWarning("Tag registration failed because this name already exists.");
            return Conflict();
        }

        var now = DateTime.UtcNow;

        var tag = new Tag
        {
            AppUserId = userId,
            Name = normalizedName,
            CreatedAt = now
        };

        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Tag name registered successful with id {TagId}", tag.Id);

        var response = new TagResponse
        {
            Id = tag.Id,
            Name = normalizedName
        };

        return StatusCode(StatusCodes.Status201Created, response);
    }

    // Return all the tags created by the current User
    [HttpGet]
    public async Task<ActionResult<List<TagResponse>>> GetTags()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim was missing or invalid.");
            return Unauthorized();
        }

        var tagsList = await _context.Tags
            .Where(tag => tag.AppUserId == userId)
            .OrderBy(tag => tag.Name)
            .Select(tag => new TagResponse
            {
                Id = tag.Id,
                Name = tag.Name
            })
            .ToListAsync();
        
        return Ok(tagsList);
    }

    // UserId Validity Check Helper
    private bool TryGetCurrentUserId(out int userId)
    {
        var userIdValue = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(userIdValue, out userId);
    }
}