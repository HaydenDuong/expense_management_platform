using System.ComponentModel.DataAnnotations;

namespace expense_management_app.Contracts.Expenses;

public class CreateTagRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}