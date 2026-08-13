using expense_management_app.Models.Identity;

namespace expense_management_app.Models.Expenses;

public class Tag
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ExpenseTag> ExpenseTags { get; set; } = [];
}