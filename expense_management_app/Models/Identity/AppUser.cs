using expense_management_app.Models.Expenses;
using expense_management_app.Models.Receipts;

namespace expense_management_app.Models.Identity;
public class AppUser
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string PasswordHash { get; set; }
    public DateTime? EmailConfirmedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // C# navigation property / convenience relationship
    public List<RefreshToken> RefreshTokens { get; set; } = [];
    public List<Expense> Expenses { get; set; } = [];
    public List<Category> Categories { get; set; } = [];
    public List<Tag> Tags { get; set; } = [];
    public List<Receipt> Receipts { get; set; } = [];
}