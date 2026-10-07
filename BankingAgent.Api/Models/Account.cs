namespace BankingAgent.Api.Models;

/// <summary>
/// Represents a bank account belonging to a customer.
/// Each customer can have one or more accounts.
/// </summary>
public class Account
{
    public int Id { get; set; }

    // Foreign key: links this account to a Customer
    public int CustomerId { get; set; }

    public string AccountNumber { get; set; } = string.Empty;

    public decimal Balance { get; set; }

    // Navigation property: EF Core uses this to load the related Customer
    public Customer Customer { get; set; } = null!;

    // Navigation property: EF Core uses this to load all transactions on this account
    public List<Transaction> Transactions { get; set; } = new();
}
