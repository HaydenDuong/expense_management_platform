namespace expense_management_app.Contracts.Expenses.Responses;

public class ExpenseResponse
{
    public int Id { get; set; }
    public string Merchant { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime ExpenseDate { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<TagResponse> Tags { get; set; } = [];
}