using expense_management_app.Models.Receipts;

namespace expense_management_app.Contracts.Receipts.Responses;

public class ReceiptResponse
{
    public int Id { get; set; }
    public ReceiptStatus Status { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}