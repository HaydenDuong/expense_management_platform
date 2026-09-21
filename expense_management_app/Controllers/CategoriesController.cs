using expense_management_app.Contracts.Expenses.Requests;
using expense_management_app.Contracts.Expenses.Responses;
using expense_management_app.Infrastructure.Persistence;
using expense_management_app.Models.Expenses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace expense_management_app.Controllers;

[ApiController]
[Authorize]
[Route("categories")]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(
        AppDbContext context,
        ILogger<CategoriesController> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    // Methods
    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> CreateCategory(
        [FromBody] CreateCategoryRequest request
    )
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim was missing or invalid.");
            return Unauthorized();
        }

        var normalizedName = request.Name.Trim().ToUpperInvariant();

        var categoryNameExist = await _context.Categories
            .AnyAsync(category => 
                category.AppUserId == userId &&
                category.Name == normalizedName);

        if (categoryNameExist)
        {
            _logger.LogWarning("Category registration rejected because this name already exists.");
            return Conflict();
        }

        var now = DateTime.UtcNow;

        var category = new Category
        {
            AppUserId = userId,
            Name = normalizedName,
            CreatedAt = now
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Category name registered successfully with id {CategoryId}", category.Id);

        var response = new CategoryResponse
        {
            Id = category.Id,
            Name = normalizedName
        };

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryResponse>>> GetCategories()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim was missing or invalid.");
            return Unauthorized();
        }

        var categoriesList = await _context.Categories
            .Where(category => category.AppUserId == userId)
            .OrderBy(category => category.Name)
            .Select(category => new CategoryResponse
            {
                Id = category.Id,
                Name = category.Name
            })
            .ToListAsync();
        
        return Ok(categoriesList);
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        var userIdValue = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(userIdValue, out userId);
    }
}