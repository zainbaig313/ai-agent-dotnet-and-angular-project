using BankingAgent.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BankingAgent.Api.Data;

/// <summary>
/// AppDbContext is the main bridge between your C# application and the SQL Server database.
/// 
/// Think of it like a "session" with the database.
/// You ask it to get/save/delete records, and it handles the SQL behind the scenes.
/// 
/// Each DbSet&lt;T&gt; property represents one table in the database.
/// For example: DbSet&lt;Customer&gt; maps to the "Customers" table.
/// </summary>
public class AppDbContext : DbContext
{
    // The constructor receives options (like the connection string) from Program.cs
    // and passes them to the base DbContext class.
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Each DbSet represents one database table.
    // You can query these like collections: _context.Customers.ToList()
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }

    /// <summary>
    /// OnModelCreating lets us configure how EF Core maps our C# classes to database tables.
    /// Here we mostly let EF Core use its default conventions, which are good enough for our project.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure the relationship: one Customer has many Accounts
        modelBuilder.Entity<Customer>()
            .HasMany(c => c.Accounts)
            .WithOne(a => a.Customer)
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Cascade); // If customer deleted, delete their accounts too

        // Configure the relationship: one Account has many Transactions
        modelBuilder.Entity<Account>()
            .HasMany(a => a.Transactions)
            .WithOne(t => t.Account)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Cascade); // If account deleted, delete its transactions too

        // Make sure Balance is stored with 18 digits, 2 decimal places (good for money)
        modelBuilder.Entity<Account>()
            .Property(a => a.Balance)
            .HasColumnType("decimal(18,2)");

        // Same for transaction amounts
        modelBuilder.Entity<Transaction>()
            .Property(t => t.Amount)
            .HasColumnType("decimal(18,2)");
    }
}
