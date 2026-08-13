using Microsoft.EntityFrameworkCore;
using expense_management_app.Models.Identity;
using expense_management_app.Models.Expenses;

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

        // B - For Expenses-Related Objects
        // For Expense Object
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.Property(expense => expense.Merchant)
                .IsRequired()
                .HasMaxLength(200);
            
            entity.Property(expense => expense.Amount)
                .IsRequired()

                // Total number of digits allowed:
                // 16 digits before decimal points
                // 2 digits after decimal points
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

                // If a category is deleted, do not delete the expenses.
                // Just set their "CategoryId" to "NULL"
                .OnDelete(DeleteBehavior.SetNull);
            
            // "Expense" vs. "Tag": many-to-many
            // Check below for "ExpenseTag" object

            // Declare indexes for "Expense" object
            // Query based on UserId and ExpenseDate
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
            
            // "Category" vs. "User: One-to-Many
            // One category belongs to one user.
            // One user can have many categories
            // Delete User -> Delete their categories
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

            // Prevent duplicate category names per user
            // This means: the same user cannot have 2 categories with the same name
            // e.g: User 7: Groceries, User 8: Groceries = fine
            //      User 7: Groceries, User 7: Groceries = is not allow
            // Note: if not including category.AppUserId => only one user in the entire app could have a "Groceries" category
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
            
            // Similar to "Category" above
            entity.HasOne(tag => tag.AppUser)
                .WithMany(user => user.Tags)
                .HasForeignKey(tag => tag.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Prevent duplicate tag names per user
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
                // This pair is the primary key
                // Prevents duplicate tag assignment
                expenseTag.ExpenseId,
                expenseTag.TagId
            });

            // Each join-row belongs to one expense
            // One expense can have many join rows, since
            // One expense can have many tags
            entity.HasOne(expenseTag => expenseTag.Expense)
                .WithMany(expense => expense.ExpenseTags)
                .HasForeignKey(expenseTag => expenseTag.ExpenseId)

                // "Cascade" is fine here because:
                // Delete expense -> delete its ExpenseTag rows
                .OnDelete(DeleteBehavior.Cascade);
            
            // Each join-row belongs to one tag
            // One Tag can have many join rows, since:
            // One tag can be used for many expenses
            entity.HasOne(expenseTag => expenseTag.Tag)
                .WithMany(tag => tag.ExpenseTags)
                .HasForeignKey(expenseTag => expenseTag.TagId)

                // "Cascade" is fine here because:
                // Delete tag -> delete its ExpenseTag rows
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}