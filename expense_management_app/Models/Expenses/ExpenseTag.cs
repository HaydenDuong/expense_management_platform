namespace expense_management_app.Models.Expenses;

public class ExpenseTag
{
    // When creating join-row, we often only know the Ids (both ExpenseId & TagId)
    // No need to attach the whole "Expense" and "Tag" objects just to create the relationship
    public int ExpenseId { get; set; }
    public Expense Expense { get; set; } = null!;
    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}