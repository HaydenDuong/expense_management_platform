namespace expense_management_app.Contracts.Receipts.Responses;

public class ReceiptListResponse
{
    public List<ReceiptResponse> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}