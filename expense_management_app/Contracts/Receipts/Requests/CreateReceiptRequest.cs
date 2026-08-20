using System.ComponentModel.DataAnnotations;

namespace expense_management_app.Contracts.Receipts.Requests;

public class CreateReceiptRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;
}