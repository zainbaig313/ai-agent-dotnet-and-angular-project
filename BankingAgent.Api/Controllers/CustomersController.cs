using BankingAgent.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankingAgent.Api.Controllers;

/// <summary>
/// CustomersController provides simple read endpoints for customer data.
/// These are mainly for testing - to verify the database is connected and has data.
/// </summary>
[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(AppDbContext context, ILogger<CustomersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/customers
    /// Returns all customers. Useful for testing that the DB is seeded.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var customers = await _context.Customers
            .Select(c => new { c.Id, c.Name, c.Email })
            .ToListAsync();

        return Ok(customers);
    }

    /// <summary>
    /// GET /api/customers/{id}
    /// Returns a single customer by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var customer = await _context.Customers
            .Where(c => c.Id == id)
            .Select(c => new { c.Id, c.Name, c.Email })
            .FirstOrDefaultAsync();

        if (customer == null)
        {
            return NotFound(new { error = $"Customer with ID {id} not found." });
        }

        return Ok(customer);
    }
}
