using expense_management_app.Models.Identity;

namespace expense_management_app.Models.Expenses;
public class Expense
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
    public required string Merchant { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    public DateTime ExpenseDate { get; set; }

    // Users can categorize expenses, but in real apps an expense may start uncategorized
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // A collection navigation should usually be an empty collection
    // Not something every caller must remember to provide
    // An expense with no tags = empty list
    public List<ExpenseTag> ExpenseTags { get; set; } = [];
}