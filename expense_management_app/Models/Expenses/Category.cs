using expense_management_app.Models.Identity;

namespace expense_management_app.Models.Expenses;

public class Category
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Expense> Expenses { get; set;} = [];
}