using expense_management_app.Models.Identity;

namespace expense_management_app.Models.Receipts;

public class Receipt
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
    public ReceiptStatus Status { get; set; }
    public required string OriginalFileName { get; set; }
    public required string StorageKey { get; set; }
    public required string ContentType { get; set; }
    public required string ContentHash { get; set; }
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}