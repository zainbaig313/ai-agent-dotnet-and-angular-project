using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BankingAgent.Api.Services;

/// <summary>
/// OpenRouterService handles all communication with the OpenRouter API.
/// 
/// OpenRouter is a service that lets you use many different AI models through one API.
/// We are using the NVIDIA Nemotron model here.
/// 
/// This service is responsible for:
/// 1. Reading your API key from configuration (not from source code!)
/// 2. Building the JSON request to send to OpenRouter
/// 3. Sending it using HttpClient
/// 4. Reading the response and returning the AI's reply
/// </summary>
public class OpenRouterService
{
    // HttpClient is a built-in .NET class for making HTTP requests (like calling a web API)
    private readonly HttpClient _httpClient;

    // IConfiguration lets us read values from appsettings.json and User Secrets
    private readonly IConfiguration _configuration;

    // ILogger lets us write log messages to the console/log files
    private readonly ILogger<OpenRouterService> _logger;

    // The AI model we want to use on OpenRouter
    private const string ModelId = "nvidia/nemotron-3-super-120b-a12b:free";

    // The OpenRouter API endpoint
    private const string ApiUrl = "https://openrouter.ai/api/v1/chat/completions";

    public OpenRouterService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenRouterService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Sends a list of messages to Nemotron and returns its reply.
    /// 
    /// The 'messages' list is the conversation history. Each message has a "role"
    /// (system, user, or assistant) and "content" (the actual text).
    /// 
    /// We can also pass in 'tools' - these are function descriptions that tell the AI
    /// what tools it can request. The AI then decides if it needs to use one.
    /// </summary>
    public async Task<OpenRouterResponse> SendChatAsync(List<ChatMessage> messages, List<ToolDefinition>? tools = null)
    {
        // Read the API key from User Secrets or environment variables - NOT from source code
        var apiKey = _configuration["OpenRouter:ApiKey"];

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(
                "OpenRouter API key is not configured. " +
                "Run: dotnet user-secrets set \"OpenRouter:ApiKey\" \"YOUR_KEY_HERE\"");
        }

        // Build the request body as a C# object (it will be serialized to JSON)
        var requestBody = new
        {
            model = ModelId,
            messages = messages,
            tools = tools,  // null means no tools for this request
            // tool_choice = "auto" means the AI decides by itself if it needs a tool
            tool_choice = tools != null ? "auto" : null
        };

        // Serialize to JSON
        var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        // Create the HTTP request
        var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Headers.Add("HTTP-Referer", "https://localhost"); // Required by OpenRouter
        request.Headers.Add("X-Title", "BankingAgent");           // Optional: names your app in OpenRouter dashboard
        request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _logger.LogInformation("Sending request to OpenRouter with model: {Model}", ModelId);

        // Send the request and wait for the response
        var httpResponse = await _httpClient.SendAsync(request);

        // Read the response body as a string
        var responseBody = await httpResponse.Content.ReadAsStringAsync();

        if (!httpResponse.IsSuccessStatusCode)
        {
            _logger.LogError("OpenRouter returned error {StatusCode}: {Body}", httpResponse.StatusCode, responseBody);
            throw new HttpRequestException($"OpenRouter error {httpResponse.StatusCode}: {responseBody}");
        }

        _logger.LogInformation("Received response from OpenRouter");

        // Deserialize the JSON response into our C# class
        var response = JsonSerializer.Deserialize<OpenRouterResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return response ?? throw new InvalidOperationException("OpenRouter returned an empty response.");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Data classes used to build requests and read responses
// These match the shape of the JSON that OpenRouter's API uses
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A single message in the conversation.
/// Role can be: "system", "user", "assistant", or "tool"
/// </summary>
public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// The name of the tool being called (only used when role = "assistant" with tool calls)
    /// </summary>
    [JsonPropertyName("tool_calls")]
    public List<ToolCall>? ToolCalls { get; set; }

    /// <summary>
    /// Used when role = "tool" to tell the AI what the tool returned
    /// </summary>
    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; set; }

    /// <summary>
    /// Convenience constructor for simple messages
    /// </summary>
    public ChatMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }

    /// <summary>
    /// Parameterless constructor needed for JSON deserialization
    /// </summary>
    public ChatMessage() { }
}

/// <summary>
/// Describes a tool (function) that the AI can choose to call.
/// We send this to OpenRouter so Nemotron knows what tools are available.
/// </summary>
public class ToolDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public FunctionDefinition Function { get; set; } = new();
}

/// <summary>
/// The detailed description of a tool/function.
/// The AI reads the Name and Description to decide when to use it.
/// Parameters tells the AI what arguments it needs to provide.
/// </summary>
public class FunctionDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public object Parameters { get; set; } = new { };
}

/// <summary>
/// Represents a tool call the AI wants to make.
/// The AI fills in the Id, the function name, and the arguments it wants to pass.
/// </summary>
public class ToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public ToolCallFunction Function { get; set; } = new();
}

/// <summary>
/// The function name and arguments from the AI's tool call.
/// Arguments is a JSON string, e.g. '{"customerId": 1}'
/// </summary>
public class ToolCallFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = string.Empty;
}

// ─────────────────────────────────────────────────────────────────────────────
// Response classes - match the JSON that OpenRouter sends back to us
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// The top-level response from OpenRouter.
/// </summary>
public class OpenRouterResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<Choice> Choices { get; set; } = new();
}

/// <summary>
/// One possible completion. OpenRouter can return multiple choices,
/// but we always use the first one (index 0).
/// </summary>
public class Choice
{
    [JsonPropertyName("message")]
    public ChatMessage Message { get; set; } = new();

    /// <summary>
    /// "stop" = the AI is done.
    /// "tool_calls" = the AI wants to call a tool before finishing.
    /// </summary>
    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; set; } = string.Empty;
}
