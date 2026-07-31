using Microsoft.EntityFrameworkCore;
using expense_management_app.Models;

namespace expense_management_app.Infrastructure.Persistence;

public class AppDbContext: DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options): base(options)
    {
        
    }

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // For AppUser object
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.Property(user => user.Email)
                .IsRequired()
                .HasMaxLength(320);
            
            entity.Property(user => user.NormalizedEmail)
                .IsRequired()
                .HasMaxLength(320);
            
            // This create an unique database index
            // Based on "user.NormalizedEmail" column
            // Prevent email duplication at database level, addition to controlller code in "/Controllers/AuthController.cs"
            entity.HasIndex(user => user.NormalizedEmail)
            .IsUnique();

            entity.Property(user => user.PasswordHash)
                .IsRequired();
            
            entity.Property(user => user.CreatedAt)
                .IsRequired();
            
            entity.Property(user => user.UpdatedAt)
                .IsRequired();
        });

        // For RefreshToken object
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.Property(token => token.TokenHash)
                .IsRequired();
            
            entity.Property(token => token.CreatedAt)
                .IsRequired();
            
            entity.Property(token => token.ExpiresAt)
                .IsRequired();
            
            // This says:
            // A RefreshToken object is belongs to one AppUser object.
            // An AppUser object can have many RefreshTokens (List<RefreshToken>)
            // RefreshToken.AppUserId is the foreign key.
            // If this user is deleted, their refresh tokens are deleted too
            entity.HasOne(token => token.AppUser)
                .WithMany(user => user.RefreshTokens)
                .HasForeignKey(token => token.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });     
    }
}