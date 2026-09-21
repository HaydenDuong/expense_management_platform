using Microsoft.EntityFrameworkCore;
using expense_management_app.Models.Identity;
using expense_management_app.Models.Expenses;
using expense_management_app.Models.Receipts;

namespace expense_management_app.Infrastructure.Persistence;

public class AppDbContext: DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options): base(options)
    {
        
    }

    // Identity
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Expenses
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ExpenseTag> ExpenseTags => Set<ExpenseTag>();

    // Receipts
    public DbSet<Receipt> Receipts => Set<Receipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // A - For Identity-Related Objects
        // For AppUser object
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.Property(user => user.Email)
                .IsRequired()
                .HasMaxLength(320);
            
            entity.Property(user => user.NormalizedEmail)
                .IsRequired()
                .HasMaxLength(320);
            
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
            
            entity.HasOne(token => token.AppUser)
                .WithMany(user => user.RefreshTokens)
                .HasForeignKey(token => token.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // B - For Expenses-Related Objects
        // For Expense Object
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.Property(expense => expense.Merchant)
                .IsRequired()
                .HasMaxLength(200);
            
            entity.Property(expense => expense.Amount)
                .IsRequired()
                .HasPrecision(18, 2);
            
            entity.Property(expense => expense.Currency)
                .IsRequired()
                .HasMaxLength(3);
            
            entity.Property(expense => expense.ExpenseDate)
                .IsRequired();
            
            entity.Property(expense => expense.Notes)
                .HasMaxLength(1000);
            
            entity.Property(expense => expense.CreatedAt)
                .IsRequired();
            
            entity.Property(expense => expense.UpdatedAt)
                .IsRequired();
            
            // Declare relationships of this object with others
            // "Expense" vs. "User": one-to-many
            entity.HasOne(expense => expense.AppUser)
                .WithMany(user => user.Expenses)
                .HasForeignKey(expense => expense.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // "Expense" vs. "Category": one-to-many
            entity.HasOne(expense => expense.Category)
                .WithMany(category => category.Expenses)
                .HasForeignKey(expense => expense.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // "Expense" vs. "Tag": many-to-many
            entity.HasIndex(expense => new
            {
                expense.AppUserId,
                expense.ExpenseDate
            });

            // Query based on UserId and CatergoryId
            entity.HasIndex(expense => new
            {
                expense.AppUserId,
                expense.CategoryId
            });

            // Query based on UserId and Merchant
            entity.HasIndex(expense => new
            {
                expense.AppUserId,
                expense.Merchant
            });
        });

        // For Category Object
        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(category => category.AppUserId)
                .IsRequired();
            
            entity.Property(category => category.Name)
                .IsRequired()
                .HasMaxLength(100);
            
            entity.Property(category => category.CreatedAt)
                .IsRequired();
            
            entity.HasOne(category => category.AppUser)
                .WithMany(user => user.Categories)
                .HasForeignKey(category => category.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Declare index for "Category" object
            entity.HasIndex(category => new
            {
                category.AppUserId,
                category.Name
            })
            .IsUnique();
        });

        // For Tag Object
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.Property(tag => tag.AppUserId)
                .IsRequired();
            
            entity.Property(tag => tag.Name)
                .IsRequired()
                .HasMaxLength(100);
            
            entity.Property(tag => tag.CreatedAt)
                .IsRequired();
            
            entity.HasOne(tag => tag.AppUser)
                .WithMany(user => user.Tags)
                .HasForeignKey(tag => tag.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(tag => new
            {
                tag.AppUserId,
                tag.Name
            })
            .IsUnique();
        });

        // For ExpenseTag Object - a relationship data
        modelBuilder.Entity<ExpenseTag>(entity =>
        {
            entity.HasKey(expenseTag => new
            {
                expenseTag.ExpenseId,
                expenseTag.TagId
            });

            entity.HasOne(expenseTag => expenseTag.Expense)
                .WithMany(expense => expense.ExpenseTags)
                .HasForeignKey(expenseTag => expenseTag.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(expenseTag => expenseTag.Tag)
                .WithMany(tag => tag.ExpenseTags)
                .HasForeignKey(expenseTag => expenseTag.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // C - For Receipts-Related Object
        // For Receipt Object
        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.Property(receipt => receipt.Status)
                .IsRequired();
            
            entity.Property(receipt => receipt.OriginalFileName)
                .IsRequired()
                .HasMaxLength(320);
            
            entity.Property(receipt => receipt.StorageKey)
                .IsRequired()
                .HasMaxLength(1024);
            
            entity.Property(receipt => receipt.ContentType)
                .IsRequired()
                .HasMaxLength(100);
            
            entity.Property(receipt => receipt.ContentHash)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(receipt => receipt.FileSize)
                .IsRequired();
            
            entity.Property(receipt => receipt.CreatedAt)
                .IsRequired();
            
            entity.Property(receipt => receipt.UpdatedAt)
                .IsRequired();
            
            // Declare its relationship to AppUser object: One to Many
            entity.HasOne(receipt => receipt.AppUser)
                .WithMany(user => user.Receipts)
                .HasForeignKey(receipt => receipt.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Declare its normal query indexes
            entity.HasIndex(receipt => new
            {
                receipt.AppUserId,
                receipt.CreatedAt
            });

            entity.HasIndex(receipt => new
            {
                receipt.AppUserId,
                receipt.Status
            });
            
            entity.HasIndex(receipt => new
            {
                receipt.AppUserId,
                receipt.ContentHash
            })
                .IsUnique();

        });
    }
}