namespace expense_management_app.Models.Identity;

public class RefreshToken
{
    public int Id { get; set; }
    public int AppUserId { get; set; } // This is the foreign key column in the DB 
    public AppUser AppUser { get; set; } = null!;  // This is the object relationship EF can load when needed
    public required string TokenHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}