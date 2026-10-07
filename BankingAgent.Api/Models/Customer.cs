namespace BankingAgent.Api.Models;

/// <summary>
/// Represents a bank customer.
/// A customer can have one or more accounts.
/// </summary>
public class Customer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    // Navigation property: EF Core uses this to load related accounts
    public List<Account> Accounts { get; set; } = new();
}
