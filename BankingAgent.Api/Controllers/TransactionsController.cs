using BankingAgent.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankingAgent.Api.Controllers;

/// <summary>
/// TransactionsController provides simple read endpoints for transaction data.
/// Mainly for testing - to verify transactions exist in the database.
/// </summary>
[ApiController]
[Route("api/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(AppDbContext context, ILogger<TransactionsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/transactions/{customerId}
    /// Returns recent transactions for a given customer.
    /// </summary>
    [HttpGet("{customerId:int}")]
    public async Task<IActionResult> GetByCustomer(int customerId)
    {
        // First find the account for this customer
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.CustomerId == customerId);

        if (account == null)
        {
            return NotFound(new { error = $"No account found for customer ID {customerId}." });
        }

        // Then get the transactions for that account
        var transactions = await _context.Transactions
            .Where(t => t.AccountId == account.Id)
            .OrderByDescending(t => t.Date)
            .Take(20)
            .Select(t => new
            {
                t.Id,
                t.Amount,
                t.Category,
                t.Description,
                t.Date,
                t.TransactionType
            })
            .ToListAsync();

        return Ok(transactions);
    }
}
