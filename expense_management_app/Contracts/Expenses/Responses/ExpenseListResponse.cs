namespace expense_management_app.Contracts.Expenses.Responses;

public class ExpenseListResponse
{
    public List<ExpenseResponse> Items { get; set;} = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}