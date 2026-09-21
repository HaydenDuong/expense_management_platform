using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using expense_management_app.Contracts.Expenses;
using expense_management_app.Contracts.Expenses.Requests;
using expense_management_app.Contracts.Expenses.Responses;
using expense_management_app.Infrastructure.Persistence;
using expense_management_app.Models.Expenses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace expense_management_app.Controllers;

[ApiController]
[Authorize]
[Route("expenses")]
public class ExpensesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<ExpensesController> _logger;
    
    public ExpensesController(
        AppDbContext context,
        ILogger<ExpensesController> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ExpenseResponse>> CreateExpense(
        [FromBody] CreateExpenseRequest request
    )
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim is missing or invalid");
            return Unauthorized();
        }

        if (request.CategoryId is not null)
        {
            var categoryExists = await _context.Categories
                .AnyAsync(category =>
                    category.Id == request.CategoryId &&
                    category.AppUserId == userId);
            
            if (!categoryExists)
            {
                _logger.LogWarning("Category does not exist for the current user.");
                return BadRequest();
            }    
        }

        var distinctTagIds = request.TagIds.Distinct().ToList();
        var tagResponses = new List<TagResponse>();

        if (distinctTagIds.Count > 0)
        {
            tagResponses = await _context.Tags
                .Where(tag =>
                    tag.AppUserId == userId &&
                    distinctTagIds.Contains(tag.Id))
                .Select(tag => new TagResponse
                {
                    Id = tag.Id,
                    Name = tag.Name
                })
                .ToListAsync();
            
            if (tagResponses.Count != distinctTagIds.Count)
            {
                _logger.LogWarning("One or more tags do not exist for the current user.");
                return BadRequest();
            }
        }

        var now = DateTime.UtcNow;
        var expense = new Expense
        {
            AppUserId = userId,
            Merchant = request.Merchant,
            Amount = request.Amount,
            Currency = request.Currency,
            ExpenseDate = request.ExpenseDate,
            CategoryId = request.CategoryId,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now,
            ExpenseTags = distinctTagIds
                .Select(tagId => new ExpenseTag
                {
                    TagId = tagId
                })
                .ToList()
        };

        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Expense has successfully registered into the database with id {ExpenseId}", expense.Id);

        var response = new ExpenseResponse
        {
            Id = expense.Id,
            Merchant = expense.Merchant,
            Amount = expense.Amount,
            Currency = expense.Currency,
            ExpenseDate = expense.ExpenseDate,
            CategoryId = expense.CategoryId,
            CategoryName = expense.Category != null? expense.Category.Name : null,
            Notes = expense.Notes,
            CreatedAt = now,
            UpdatedAt = now,
            Tags = tagResponses
        };

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    public async Task<ActionResult<ExpenseListResponse>> GetExpenses(
        [FromQuery] ExpenseQueryParameters query)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim was missing or invalid.");
            return Unauthorized();
        }

        var expensesQuery = _context.Expenses
            .Where(expense => expense.AppUserId == userId);
        
        if (query.FromDate is not null)
        {
            expensesQuery = expensesQuery
                .Where(expense => expense.ExpenseDate >= query.FromDate.Value);
        }

        if (query.ToDate is not null)
        {
            expensesQuery = expensesQuery
                .Where(expense => expense.ExpenseDate <= query.ToDate.Value);
        }
        
        if (query.CategoryId is not null)
        {
            expensesQuery = expensesQuery
                .Where(expense => expense.CategoryId == query.CategoryId);
        }

        if (!string.IsNullOrWhiteSpace(query.Merchant))
        {
            var merchant = query.Merchant.Trim();

            expensesQuery = expensesQuery
                .Where(expense => expense.Merchant.Contains(merchant));
        }

        if (query.MinAmount is not null)
        {
            expensesQuery = expensesQuery
                .Where(expense => expense.Amount >= query.MinAmount.Value);
        }

        if (query.MaxAmount is not null)
        {
            expensesQuery = expensesQuery
                .Where(expense => expense.Amount <= query.MaxAmount.Value);
        }

        var totalCount = await expensesQuery.CountAsync();

        expensesQuery = query.SortBy.ToLowerInvariant() switch
        {
            "amount" => query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
                ? expensesQuery.OrderBy(expense => expense.Amount)
                : expensesQuery.OrderByDescending(expense => expense.Amount),

            "merchant" => query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
                ? expensesQuery.OrderBy(expense => expense.Merchant)
                : expensesQuery.OrderByDescending(expense => expense.Merchant),

            _ => query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
                ? expensesQuery.OrderBy(expense => expense.ExpenseDate)
                : expensesQuery.OrderByDescending(expense => expense.ExpenseDate)
        };

        var page = Math.Max(query.Page, 1);                     
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        expensesQuery = expensesQuery
            .Skip(skip)
            .Take(pageSize);

        var items = await expensesQuery
            .Select(expense => new ExpenseResponse
            {
                Id = expense.Id,
                Merchant = expense.Merchant,
                Amount = expense.Amount,
                Currency = expense.Currency,
                ExpenseDate = expense.ExpenseDate,
                CategoryId = expense.CategoryId,
                CategoryName = expense.Category != null ? expense.Category.Name : null,
                Notes = expense.Notes,
                CreatedAt = expense.CreatedAt,
                UpdatedAt = expense.UpdatedAt,
                Tags = expense.ExpenseTags
                    .Select(expenseTag => new TagResponse
                    {
                        Id = expenseTag.TagId,
                        Name = expenseTag.Tag.Name
                    })
                    .ToList()
            })
            .ToListAsync();
        
        var response = new ExpenseListResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return Ok(response);
    }

    [HttpGet("{expenseId}")]
    public async Task<ActionResult<ExpenseResponse>> GetOneExpense(
        [FromRoute] int expenseId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim is missing or invalid.");
            return Unauthorized();
        }

        var response = await _context.Expenses
            .Where(expense =>
                expense.AppUserId == userId &&
                expense.Id == expenseId)
            .Select(expense => new ExpenseResponse
            {
                Id = expense.Id,
                Merchant = expense.Merchant,
                Amount = expense.Amount,
                Currency = expense.Currency,
                ExpenseDate = expense.ExpenseDate,
                CategoryId = expense.CategoryId,
                CategoryName = expense.Category != null? expense.Category.Name : null,
                Notes = expense.Notes,
                CreatedAt = expense.CreatedAt,
                UpdatedAt = expense.UpdatedAt,
                Tags = expense.ExpenseTags
                    .Select(expenseTag => new TagResponse
                    {
                        Id = expenseTag.TagId,
                        Name = expenseTag.Tag.Name
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();
        
        if (response is null)
        {
            _logger.LogWarning("Request rejected because this expense is not exist or invalid.");
            return NotFound();
        }
        
        return Ok(response);
    }

    [HttpDelete("{expenseId}")]
    public async Task<ActionResult> DeleteExpense(
        [FromRoute] int expenseId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim is missing or invalid.");
            return Unauthorized();
        }

        var expense = await _context.Expenses
            .FirstOrDefaultAsync(expense => 
                expense.AppUserId == userId &&
                expense.Id == expenseId);
        
        if (expense is null)
        {
            _logger.LogWarning("Request rejected because this Expense ID does not exist or invalid.");
            return NotFound();
        }

        _context.Expenses.Remove(expense);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Delete request for this expense is successed.");

        return NoContent();
    }

    [HttpPut("{expenseId}")]
    public async Task<ActionResult<ExpenseResponse>> UpdateExpense(
        [FromRoute] int expenseId,
        [FromBody] UpdateExpenseRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim is missing or invalid");
            return Unauthorized();
        }

        var expense = await _context.Expenses
            .Include(expense => expense.ExpenseTags)
            .FirstOrDefaultAsync(expense =>
                expense.AppUserId == userId &&
                expense.Id == expenseId);
        
        if (expense is null)
        {
            _logger.LogWarning("Request rejected because this Expense ID does not exist or invalid.");
            return NotFound();
        }

        if (request.CategoryId is not null)
        {
            var categoryExist = await _context.Categories
                .AnyAsync(category =>
                    category.AppUserId == userId &&
                    category.Id == request.CategoryId);
            
            if (!categoryExist)
            {
                _logger.LogWarning("Category does not exist for the current user.");
                return BadRequest();
            }
        }

        var distinctTagIds = request.TagIds.Distinct().ToList();
        var tagResponses = new List<TagResponse>();

        if (distinctTagIds.Count > 0)
        {
            tagResponses = await _context.Tags
                .Where(tag =>
                    tag.AppUserId == userId &&
                    distinctTagIds.Contains(tag.Id))
                .Select(tag => new TagResponse
                {
                    Id = tag.Id,
                    Name = tag.Name
                })
                .ToListAsync();

            if (tagResponses.Count != distinctTagIds.Count)
            {
                _logger.LogWarning("One or more tags do not exist for the current user.");
                return BadRequest();
            }
        }

        var now = DateTime.UtcNow;

        expense.Merchant = request.Merchant;
        expense.Amount = request.Amount;
        expense.Currency = request.Currency;
        expense.ExpenseDate = request.ExpenseDate;
        expense.CategoryId = request.CategoryId;
        expense.Notes = request.Notes;
        expense.UpdatedAt = now;

        expense.ExpenseTags.Clear();
        foreach (var tagId in distinctTagIds)
        {
            expense.ExpenseTags.Add(new ExpenseTag
            {
                ExpenseId = expense.Id,
                TagId = tagId
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("This expense is successfully updated.");

        var response = new ExpenseResponse
        {
            Id = expense.Id,
            Merchant = expense.Merchant,
            Amount = expense.Amount,
            Currency = expense.Currency,
            ExpenseDate = expense.ExpenseDate,
            CategoryId = expense.CategoryId,
            CategoryName = expense.Category != null ? expense.Category.Name : null,
            Notes = expense.Notes,
            CreatedAt = expense.CreatedAt,
            UpdatedAt = expense.UpdatedAt,
            Tags = tagResponses
        };

        return Ok(response);
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        var userIdValue = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(userIdValue, out userId);
    }
}