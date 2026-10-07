namespace BankingAgent.Api.Models;

/// <summary>
/// Represents a single financial transaction on an account.
/// Amount is always positive. TransactionType tells us if it was a debit or credit.
/// </summary>
public class Transaction
{
    public int Id { get; set; }

    // Foreign key: links this transaction to an Account
    public int AccountId { get; set; }

    /// <summary>
    /// Always stored as a positive number (e.g., 500.00).
    /// Use TransactionType to know if money came in or went out.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Category helps with spending summaries.
    /// Examples: "Food", "Utilities", "Transport", "Shopping"
    /// </summary>
    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    /// <summary>
    /// "Debit" means money left the account (spending).
    /// "Credit" means money entered the account (income/deposit).
    /// </summary>
    public string TransactionType { get; set; } = "Debit";

    // Navigation property: EF Core uses this to load the related Account
    public Account Account { get; set; } = null!;
}
