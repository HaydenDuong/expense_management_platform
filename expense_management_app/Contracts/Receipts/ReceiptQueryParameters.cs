using expense_management_app.Models.Receipts;

namespace expense_management_app.Contracts.Receipts;

public class ReceiptQueryParameters
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public DateTime? FromCreatedAt { get; set; }
    public DateTime? ToCreatedAt { get; set; }
    public ReceiptStatus? Status { get; set; } 
    public string SortBy { get; set; } = "createdAt";
    public string SortDirection { get; set; } = "desc";
}