using BankingAgent.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BankingAgent.Api.Controllers;

/// <summary>
/// AgentController is the main entry point for the AI banking agent.
///
/// What is a Controller?
/// ─────────────────────
/// A controller is a class that receives HTTP requests and returns HTTP responses.
/// ASP.NET Core looks at the URL and routes the request to the right controller method.
///
/// [ApiController]      → Tells ASP.NET Core this is an API controller (enables auto-validation, etc.)
/// [Route("api/agent")] → All endpoints in this controller start with /api/agent
/// </summary>
[ApiController]
[Route("api/agent")]
public class AgentController : ControllerBase
{
    // AgentService does the actual work. We receive it via Dependency Injection.
    // We don't "new" it ourselves - ASP.NET Core creates it and passes it in.
    private readonly AgentService _agentService;
    private readonly ILogger<AgentController> _logger;

    public AgentController(AgentService agentService, ILogger<AgentController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/agent
    ///
    /// The Angular frontend will call this endpoint when the user sends a message.
    ///
    /// Request body example:
    /// {
    ///   "customerId": 1,
    ///   "message": "What is my balance?"
    /// }
    ///
    /// Response example:
    /// {
    ///   "response": "Your current balance is Rs. 185,000."
    /// }
    ///
    /// [HttpPost] → This method responds to HTTP POST requests
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] AgentRequest request)
    {
        // Basic validation
        if (request.CustomerId <= 0)
        {
            return BadRequest(new { error = "CustomerId must be a positive number." });
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message cannot be empty." });
        }

        _logger.LogInformation("Agent chat request from customer {CustomerId}", request.CustomerId);

        try
        {
            // Delegate all the AI logic to AgentService - the controller stays thin
            var aiResponse = await _agentService.ChatAsync(request.CustomerId, request.Message);

            // Return the response in a simple JSON format Angular can consume easily
            return Ok(new AgentResponse { Response = aiResponse });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API key"))
        {
            _logger.LogError("OpenRouter API key is not configured");
            return StatusCode(500, new { error = "AI service is not configured. Please set the OpenRouter API key." });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to communicate with OpenRouter");
            return StatusCode(502, new { error = "Failed to reach the AI service. Please try again later." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in agent chat");
            return StatusCode(500, new { error = "An unexpected error occurred." });
        }
    }
}

/// <summary>
/// The request body the frontend sends to POST /api/agent
/// </summary>
public class AgentRequest
{
    /// <summary>The ID of the logged-in customer</summary>
    public int CustomerId { get; set; }

    /// <summary>The customer's message/question</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// The response the API sends back to the frontend
/// </summary>
public class AgentResponse
{
    /// <summary>The AI's natural-language answer</summary>
    public string Response { get; set; } = string.Empty;
}
