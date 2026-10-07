using BankingAgent.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankingAgent.Api.Controllers;

/// <summary>
/// AccountsController provides simple read endpoints for account data.
/// Mainly for testing - to verify accounts exist in the database.
/// </summary>
[ApiController]
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(AppDbContext context, ILogger<AccountsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/accounts/{customerId}
    /// Returns the account for a given customer.
    /// </summary>
    [HttpGet("{customerId:int}")]
    public async Task<IActionResult> GetByCustomer(int customerId)
    {
        var account = await _context.Accounts
            .Where(a => a.CustomerId == customerId)
            .Select(a => new
            {
                a.Id,
                a.AccountNumber,
                a.Balance,
                a.CustomerId
            })
            .FirstOrDefaultAsync();

        if (account == null)
        {
            return NotFound(new { error = $"No account found for customer ID {customerId}." });
        }

        return Ok(account);
    }
}
