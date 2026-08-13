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
    // Dependencies
    private readonly AppDbContext _context;
    private readonly ILogger<ExpensesController> _logger;
    
    // Constructor
    public ExpensesController(
        AppDbContext context,
        ILogger<ExpensesController> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    // HTTP Endpoints
    // Create an expense object for the current UserId
    [HttpPost]
    public async Task<ActionResult<ExpenseResponse>> CreateExpense(
        [FromBody] CreateExpenseRequest request
    )
    {
        // UserId validity check
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim is missing or invalid");
            return Unauthorized();
        }

        // Validity Checks for both CategoryId and TagId
        if (request.CategoryId is not null)
        {
            // Check if this category exists (through the provided request.CategoryId)
            // and whether this user already have it registered with his / her account.
            var categoryExists = await _context.Categories
                .AnyAsync(category =>
                    category.Id == request.CategoryId &&
                    category.AppUserId == userId);
            
            // In the current approach, the categoryId must exist before expense is created
            // Thus, if the input categoryId is not exist in the Categories table => Reject with BadRequest()
            if (!categoryExists)
            {
                _logger.LogWarning("Category does not exist for the current user.");
                return BadRequest();
            }    
        }

        // Client could sent duplicate TagIds like: "tagIds": [1, 1, 2]
        // Using "Distinct()" here will result in: [1, 2] => we remove duplicate before creating join rows for ExpenseTag
        // The request only sends tag IDs, but the database stores expense/tag links
        // as ExpenseTag join rows. 
        // First remove duplicate tag IDs so we do not try
        // to create duplicate (ExpenseId, TagId) pairs. 
        // Then verify every requested tag belongs to the current user before attaching it to the new expense.
        var distinctTagIds = request.TagIds.Distinct().ToList();
        var tagResponses = new List<TagResponse>();

        if (distinctTagIds.Count > 0)
        {
            // "tagResponses" does 2 jobs:
            // 1. Validation
            // If count does not match => at least one tag id was invalid.
            // 2. Response:
            // It already has Id and Name => ready to return in response.
            // A.k.a: fetch valid tags owned by the current userId (those must present in distinctTagIds was well)
            tagResponses = await _context.Tags

                // Ask Database to find those tags that are created by this userId
                // and these must also appear in "distinctTagIds" as well
                // Technical:
                // Find the requested tags that belong to the current userId
                // This both protects ownership and gives us the tag data needed
                // for the response body.
                .Where(tag =>
                    tag.AppUserId == userId &&
                    
                    // We cannot use "here" because distinctTagIds is a list
                    // So the following code is more efficient as it compares:
                    // whether tag.Id == distinctTagIds[0] OR tag.Id == distinctTagIds[1] OR ...
                    distinctTagIds.Contains(tag.Id))
                
                // Pick those and create TagResponse objects out of them
                // And store them in this List named "tagResponses"
                // Technical:
                // Project database Tag entities into API response DTOs.
                // We do not want to return Tag entities directly because they contain
                // internal fields such as AppUserId and navigation properties (dangerous if leaked out)
                .Select(tag => new TagResponse
                {
                    Id = tag.Id,
                    Name = tag.Name
                })
                .ToListAsync();
            
            // If fewer tags came back than were requested => at least one tag ID
            // either does not exist or belongs to another user.
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

            // Convert requested tag IDs into ExpenseTag join rows. 
            // Purpose: join rows for saving to DB
            // ExpenseId is not set here because the Expense has not been inserted yet. 
            // EF Core will generate the Expense.Id first, then use it for these related join rows.
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

            // "tagResponses" need to be created first or else it cannot be returned like this
            // a.k.a: even if the request has no tags, we still want "tags":[]
            // we cannot use ExpenseTags because that is database relationship entity.
            // tagResponses = API response-shape (JSON-format) entity => suitable for return in response.
            // distinctTagIds = used to create ExpenseTag
            Tags = tagResponses
        };

        return StatusCode(StatusCodes.Status201Created, response);
    }

    // Retrieving all current userId's expenses that match the request query paramaters
    // Note: Order of query parameters do not matter, however, names are matter.
    [HttpGet]
    public async Task<ActionResult<ExpenseListResponse>> GetExpenses(
        [FromQuery] ExpenseQueryParameters query)
    {
        // UserId validity check
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim was missing or invalid.");
            return Unauthorized();
        }

        // For filtering, sorting, pagination processes:
        // We want to build a SQL query to send to PostGreSQL DB
        // to let it perform that query and return us with the requested result.
        
        // If we decided to retrieve all the expenses of the current userId into our server right now
        // Then we will need to do filter / sort / paginate after that with:
        // expenses = expenses
        //      .Where(...)
        //      .Skip(...)
        //      .Take(...)
        //      .ToList(...)
        // Which is hurting server memory (C#), and not efficient compare to PostGreSQL which is optimized for those
        // Because we only want our API receives the result it needs
        // Leave the heavy lifting for PostGreSQL.
        // This var "expensesQuery" is an IQueryable<Expense> - it represents a database query being built.
        // EF CORE has not sent this SQL query to PostgreSQL yet.
        // This SQL query is executed later when we call on of these:
        // CountAsync(), ToListAsync(), FirstOrDefaultAsync(), AnyAsync(), etc.

        // 1. Start with all expenses for the current userId
        // ~ SQL: "SELECT *
        //         FROM Expenses
        //         WHERE AppUserId = @userId
        var expensesQuery = _context.Expenses
            .Where(expense => expense.AppUserId == userId);
        
        // 2. Apply filters if the client provided them

        // A. Add Date Filters
        // e.g: GET /expenses?fromDate=2026-08-01&toDate=2026-08-31
        // Means: Only expenses in August 2026
        // ~ SQL: "SELECT * 
        //         FROM Expenses
        //         WHERE AppUserId = @userId
        //         AND ExpenseDate >= @FromDate AND ExpenseDate <= @ToDate
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
        
        // B. Add Category Filter
        // e.g: GET /expenses?fromDate=2026-08-01&toDate=2026-08-31&categoryId=3
        // Means: Only current user's expenses in this category, e.g, categoryId=3 is grocery => find of grocery expenses of this user
        // ~ SQL: "SELECT *
        //         FROM Expenses
        //         WHERE AppUserId = @userId
        //         AND ExpenseDate >= @FromFate AND ExpenseDate <= @ToDate
        //         AND CategoryId = @categoryId
        // However, if the provided categoryId belongs to another user, then:
        // it simply returns 0 rows because base query already scoped by AppUserId
        if (query.CategoryId is not null)
        {
            expensesQuery = expensesQuery
                .Where(expense => expense.CategoryId == query.CategoryId);
        }

        // C. Add Merchant Filter
        // e.g: GET /expenses?fromDate=2026-08-01&toDate=2026-08-31&categoryId=3&categoryId=3&merchant=woolworth
        // ~ SQL: "SELECT *
        //         FROM Expenses
        //         WHERE AppUserId = @userId
        //         AND ExpenseDate >= @FromFate AND ExpenseDate <= @ToDate
        //         AND CategoryId = @categoryId
        //         AND Merchant LIKE '%merchant%'
        // For Merchant filter, client sometimes can send merchant=wool instead of woolworth
        // Thus, "Contains" is used for substring match / search that closer to the proved input, but not exact equality
        // EF Core "Contains" is translated into "LIKE" in SQL
        // In addition, client may send as GET/ expenses?merchant=  => !string.IsNullOrWhiteSpace(query.Merchant) is used to for that
        // because we do not want to filter by an empty / blank string.
        // Note:
        // Because "merchant" is a string => String.Contains(text) = does this "text" contains a substring of "String"
        // While List.Contains(value), e.g: distinctTagIds.Contains(tag.Id) = is this "tagId" equal to one of the values present in distinctTagIds
        if (!string.IsNullOrWhiteSpace(query.Merchant))
        {
            var merchant = query.Merchant.Trim();

            expensesQuery = expensesQuery
                .Where(expense => expense.Merchant.Contains(merchant));
        }

        // D. Add Amount Filter
        // e.g: GET /expenses?fromDate=2026-08-01&toDate=2026-08-31&categoryId=3&categoryId=3&merchant=woolworth&minAmount=10&maxAmount=50
        // Means: Only expenses between 10 and 50
        // ~ SQL: "SELECT *
        //         FROM Expenses
        //         WHERE AppUserId = @userId
        //         AND ExpenseDate >= @FromFate AND ExpenseDate <= @ToDate
        //         AND CategoryId = @categoryId
        //         AND Merchant LIKE '%merchant%'
        //         AND MinAmount >= 10 AND MaxAmount <= 50
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

        // 3. Count how many rows match before pagination
        var totalCount = await expensesQuery.CountAsync();

        // 4. Apply Sorting based on query.SortBy and query.SortDirection
        // e.g: GET /expenses?sortBy=amount&sortDirection=asc
        // Means: Sort expenses by amount, smallest to largest
        // Since client may send in parameter value like: "Amount", or "AMOUNT", or even "amount" 
        // => .ToLowerInvariant() is used to normalize = easy to work around
        // "switch" just like "switch" in Python
        // _ == anything else other than "amount" and "merchant", e.g: GET /expenses?sortBy=random
        // In this case, we will fall back to use "expenseDate" as sorting criteria instead
        // => safer than trying to dynamically sort by arbitray input
        expensesQuery = query.SortBy.ToLowerInvariant() switch
        {
            // This says:
            // If sortBy is amount:
            //   If sortDirection is asc:
            //     order by Amount ascending.
            // Else:
            //     order by Amount descending.
            // OrdinalIgnoreCase = ignoring casing
            // Since client may send: asc, ASC, Asc, aSc, etc. 
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

        // 5. Apply pagination
        // Goal: Do not return every matching expense => Return only one page of result
        // e.g: GET /expenses?page=2&pageSize=10 == Give me the second group of 10 expenses.
        // Remember: always sorted before pagination
        // Because: pagination without sorting is unstable
        // Thus, the correct order to follow:
        // Filter => Count => Sort => Skip / Take => Select => ToListAsync()
        // Here just think of: 
        //      How many items you want to see in page and server wil "cut" those retrieved results into a book of pages make up the requested number of those items
        //      Then, which page from that book you want to see
        // Thus, query order is not matter but names does
        var page = Math.Max(query.Page, 1);                     // If query.Page is < 1, use 1. Otherwise, user query.Page
        var pageSize = Math.Clamp(query.PageSize, 1, 100);      // Keep pageSize between 1 and 100 => if value < min, use min; if value is > max; otherwise user value.
        
        // This calculate how many rows to skip before taking the current page (requested page)
        // e.g:
        // Page 1:
        // skip = (1 - 1) * 10 = 0
        // Skip 0, take 10
        // Rows 1-10

        // Page 2:
        // skip = (2 - 1) * 10 = 10
        // Skip 10, take 10
        // Rows 11-20

        // Page 3:
        // skip = (3 - 1) * 10 = 20
        // Skip 20, take 10
        // Rows 21-30

        // Thus:
        // Page 1 starts after 0 rows
        // Page 2 starts after 10 rows
        // Page 3 starts after 20 rows
        var skip = (page - 1) * pageSize;

        // This means:
        // page = 1, pageSize = 20 -> skip 0, take 20
        // page = 2, pageSize = 20 -> skip 20, take 20
        // page = 3, pageSize = 20 -> skip 40, take 20
        expensesQuery = expensesQuery

            // Skip() == ignores this many rows from the beginning
            // ~ SQL: OFFSET @skip
            .Skip(skip)

            // Take() == return only these many rows
            // ~ SQL: LIMIT @pageSize
            .Take(pageSize);

            // e.g: .Skip(10).Take(10) == skip first 10 rows, return the next 10

        // 6. Project selected expenses entities that matching the above criterias into ExpenseResponse DTOs
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
        
        // 7. Return List Response
        var response = new ExpenseListResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return Ok(response);
    }

    // Retrieving one expense from current userId's expenses based on input expenseId
    [HttpGet("{expenseId}")]
    public async Task<ActionResult<ExpenseResponse>> GetOneExpense(
        [FromRoute] int expenseId)
    {
        // UserId validity check
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim is missing or invalid.");
            return Unauthorized();
        }

        // ExpenseId validity check
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

    // Delete an expense object of the current userId based on the input ExpenseId
    [HttpDelete("{expenseId}")]
    public async Task<ActionResult> DeleteExpense(

        // Can be wrote as [FromQuery] if we going to use: DELETE /expenses?expenseId=123
        // Since, we decided to go with DELETE /expenses/123 => this is [FromRoute]
        [FromRoute] int expenseId)
    {
        // UserId validity check
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim is missing or invalid.");
            return Unauthorized();
        }

        // Find the expense only if it belongs to the current userId.
        // If not found, return 404 to prevent the leakage of other user's expenses exists.
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(expense => 
                expense.AppUserId == userId &&
                expense.Id == expenseId);
        
        if (expense is null)
        {
            _logger.LogWarning("Request rejected because this Expense ID does not exist or invalid.");
            return NotFound();
        }

        // Execute deletion for this expense and save changes to PostGreSQL DB
        _context.Expenses.Remove(expense);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Delete request for this expense is successed.");

        return NoContent();
    }

    // Update an existing expense through input expenseId
    [HttpPut("{expenseId}")]
    public async Task<ActionResult<ExpenseResponse>> UpdateExpense(
        [FromRoute] int expenseId,
        [FromBody] UpdateExpenseRequest request)
    {
        // UserId validity check
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim is missing or invalid");
            return Unauthorized();
        }

        // Expense validity check
        var expense = await _context.Expenses

            // This will include the existing "ExpenseTags" rows to this object "expense" if found
            // Since one expense can have many tag => "ExpenseTags" rows being registered for this "expense"
            .Include(expense => expense.ExpenseTags)
            .FirstOrDefaultAsync(expense =>
                expense.AppUserId == userId &&
                expense.Id == expenseId);
        
        if (expense is null)
        {
            _logger.LogWarning("Request rejected because this Expense ID does not exist or invalid.");
            return NotFound();
        }

        // Validity check for input CategoryId from client request
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

        // Validity check for input TagIds from client request
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

        // Update the retrieved expense fields with request fields
        expense.Merchant = request.Merchant;
        expense.Amount = request.Amount;
        expense.Currency = request.Currency;
        expense.ExpenseDate = request.ExpenseDate;
        expense.CategoryId = request.CategoryId;
        expense.Notes = request.Notes;
        expense.UpdatedAt = now;

        // Remove all loaded links and add the new set of links
        expense.ExpenseTags.Clear();
        foreach (var tagId in distinctTagIds)
        {
            expense.ExpenseTags.Add(new ExpenseTag
            {
                ExpenseId = expense.Id,
                TagId = tagId
            });
        }

        // Since the current expense was loaded / tracked by EF, thus, the following .Update(expense) is optional
        //_context.Expenses.Update(expense);

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