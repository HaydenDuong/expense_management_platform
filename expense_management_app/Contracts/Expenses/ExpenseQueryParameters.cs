namespace expense_management_app.Contracts.Expenses;

public class ExpenseQueryParameters
{
    // Eventhough some values are hard-coded.
    // But this is useful because ListExpenseResponse endpoints should behave predictably
    // even when the client gives no filter like: GET /expenses
    // This is like the default listing rule - which can be changed through GET /expense "parameter go here"
    // e.g: GET /expenses?page=2&pageSize=10&fromDate=2026-08-01&toDate=2026-08-31&merchant=woolworths&sortBy=amount&sortDirection=desc
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? CategoryId { get; set; }
    public string? Merchant { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }

    public string SortBy { get; set; } = "expenseDate";
    public string SortDirection { get; set; } = "desc";
}