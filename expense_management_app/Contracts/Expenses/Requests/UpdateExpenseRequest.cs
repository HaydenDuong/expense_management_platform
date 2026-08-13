using System.ComponentModel.DataAnnotations;

namespace expense_management_app.Contracts.Expenses.Requests;

public class UpdateExpenseRequest
{
    [Required]
    [MaxLength(200)]
    public string Merchant { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = string.Empty;

    [Required]
    public DateTime ExpenseDate { get; set; }

    public int? CategoryId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public List<int> TagIds { get; set; } = [];
}