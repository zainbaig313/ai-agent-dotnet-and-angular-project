using System.Text.Json;
using BankingAgent.Api.Data;
using BankingAgent.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BankingAgent.Api.Services;

/// <summary>
/// AgentService is the brain of the AI banking agent.
///
/// What makes this an "agent" (not just a chatbot)?
/// ─────────────────────────────────────────────────
/// A regular chatbot just sends a message to the AI and gets text back.
///
/// An AI *agent* can also call real functions (tools) to get live data.
/// The AI reads the user's question, decides it needs real information,
/// asks us to run a specific function, we run it, send the result back to the AI,
/// and the AI uses that result to write a proper answer.
///
/// Tool-calling loop (simplified):
///   1. User asks "What is my balance?"
///   2. We send the question to Nemotron, together with a list of available tools
///   3. Nemotron replies: "I want to call GetBalance with customerId=1"
///   4. We actually call our GetBalance() C# method
///   5. GetBalance() queries SQL Server and returns Rs. 185,000
///   6. We send that result back to Nemotron
///   7. Nemotron writes: "Your current balance is Rs. 185,000."
///   8. We return that final text to the user
/// </summary>
public class AgentService
{
    private readonly OpenRouterService _openRouterService;
    private readonly AppDbContext _context;
    private readonly ILogger<AgentService> _logger;

    public AgentService(OpenRouterService openRouterService, AppDbContext context, ILogger<AgentService> logger)
    {
        _openRouterService = openRouterService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Main entry point. The controller calls this with the customer's ID and their message.
    /// Returns the AI's final natural-language answer.
    /// </summary>
    public async Task<string> ChatAsync(int customerId, string userMessage)
    {
        _logger.LogInformation("Agent received message from customer {CustomerId}: {Message}", customerId, userMessage);

        // ── Step 1: Build the conversation ─────────────────────────────────
        // "system" message: tells the AI its role and rules
        // "user" message: the customer's actual question
        var messages = new List<ChatMessage>
        {
            new ChatMessage("system",
                $"You are a helpful banking assistant. " +
                $"The customer ID is {customerId}. " +
                $"When the user asks about their account, balance, or transactions, " +
                $"use the available tools to get real data before answering. " +
                $"Always use Pakistani Rupees (Rs.) for amounts. " +
                $"Be concise and friendly. " +
                $"If a tool reports that the customer or account is not found, state that politely in plain text. " +
                $"Never output XML, raw tool tags, or call nonexistent tools."),

            new ChatMessage("user", userMessage)
        };

        // ── Step 2: Define the tools the AI can request ─────────────────────
        // This list tells Nemotron what functions exist and when to use them.
        // The AI reads the "description" to decide which tool matches the user's question.
        var tools = GetToolDefinitions();

        // ── Step 3: First call to OpenRouter ───────────────────────────────
        // We send the conversation + tool definitions. The AI may respond with
        // a normal text answer OR with a tool_call request.
        var firstResponse = await _openRouterService.SendChatAsync(messages, tools);
        var firstChoice = firstResponse.Choices.FirstOrDefault()
            ?? throw new InvalidOperationException("OpenRouter returned no choices.");

        // ── Step 4: Handle tool calls (the "agentic" part) ─────────────────
        // If the AI decided it needs a tool, finishReason will be "tool_calls"
        if (firstChoice.FinishReason == "tool_calls" && firstChoice.Message.ToolCalls != null)
        {
            _logger.LogInformation("AI requested tool calls. Processing...");

            // Add the AI's tool-call message to the conversation history
            messages.Add(firstChoice.Message);

            // Execute each tool the AI requested and add the results to the conversation
            foreach (var toolCall in firstChoice.Message.ToolCalls)
            {
                _logger.LogInformation("AI wants to call tool: {ToolName} with args: {Args}",
                    toolCall.Function.Name, toolCall.Function.Arguments);

                // Run the actual C# function and get the result
                var toolResult = await ExecuteToolAsync(customerId, toolCall);

                // Add the tool result to the conversation so the AI can see it
                messages.Add(new ChatMessage
                {
                    Role = "tool",
                    ToolCallId = toolCall.Id,
                    Content = toolResult
                });
            }

            // ── Step 5: Second call to OpenRouter ─────────────────────────
            // Now the AI has the real data from our tools. It will use that
            // data to write a proper natural-language answer.
            var finalResponse = await _openRouterService.SendChatAsync(messages);
            var finalChoice = finalResponse.Choices.FirstOrDefault()
                ?? throw new InvalidOperationException("OpenRouter returned no choices on second call.");

            var finalContent = finalChoice.Message.Content;

            // If the model produced raw tool markup or null, provide a clean friendly fallback
            if (string.IsNullOrWhiteSpace(finalContent) || finalContent.Contains("<tool_call>", StringComparison.OrdinalIgnoreCase))
            {
                return $"Customer with ID {customerId} was not found. Please verify your customer ID.";
            }

            return finalContent;
        }

        // If the AI didn't need any tools, just return its direct answer
        var directContent = firstChoice.Message.Content;
        if (string.IsNullOrWhiteSpace(directContent) || directContent.Contains("<tool_call>", StringComparison.OrdinalIgnoreCase))
        {
            return $"I could not find account information for customer ID {customerId}. Please verify your details.";
        }

        return directContent;
    }

    /// <summary>
    /// Returns the list of tool definitions we send to the AI.
    /// Each tool has a name, a description (the AI reads this to decide when to use it),
    /// and a list of parameters (what arguments the AI needs to provide).
    /// </summary>
    private List<ToolDefinition> GetToolDefinitions()
    {
        return new List<ToolDefinition>
        {
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "GetBalance",
                    Description = "Get the current account balance for the customer. Use this when the user asks about their balance, how much money they have, or their account funds.",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            customerId = new
                            {
                                type = "integer",
                                description = "The ID of the customer"
                            }
                        },
                        required = new[] { "customerId" }
                    }
                }
            },

            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "GetRecentTransactions",
                    Description = "Get the recent transaction history for the customer. Use this when the user asks about recent transactions, payment history, or what they spent money on recently.",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            customerId = new
                            {
                                type = "integer",
                                description = "The ID of the customer"
                            }
                        },
                        required = new[] { "customerId" }
                    }
                }
            },

            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "GetSpendingSummary",
                    Description = "Get a summary of the customer's total spending, broken down by category. Use this when the user asks about spending habits, how much they spent this month, or spending by category like food or transport.",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            customerId = new
                            {
                                type = "integer",
                                description = "The ID of the customer"
                            }
                        },
                        required = new[] { "customerId" }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Routes the AI's tool call request to the correct C# method.
    /// Returns the result as a plain string so the AI can read it.
    /// 
    /// The AI cannot directly run our code. It just says "please call GetBalance with customerId=1".
    /// We then run the actual code and send the result back.
    /// </summary>
    private async Task<string> ExecuteToolAsync(int customerId, ToolCall toolCall)
    {
        // Parse the JSON arguments the AI provided
        // For example: '{"customerId": 1}'  →  JsonDocument
        using var argsDoc = JsonDocument.Parse(toolCall.Function.Arguments);

        return toolCall.Function.Name switch
        {
            "GetBalance" => await GetBalanceAsync(customerId),
            "GetRecentTransactions" => await GetRecentTransactionsAsync(customerId),
            "GetSpendingSummary" => await GetSpendingSummaryAsync(customerId),
            _ => $"Unknown tool: {toolCall.Function.Name}"
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TOOL IMPLEMENTATIONS
    // These are the real banking functions that query the SQL Server database.
    // The AI cannot run these directly - it can only request them by name.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tool: GetBalance
    /// Finds the customer's account and returns the current balance.
    /// </summary>
    private async Task<string> GetBalanceAsync(int customerId)
    {
        _logger.LogInformation("Executing GetBalance for customer {CustomerId}", customerId);

        // Find the customer first to make sure they exist
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer == null)
        {
            return $"Customer with ID {customerId} was not found.";
        }

        // Find the customer's account
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.CustomerId == customerId);

        if (account == null)
        {
            return $"No account found for customer {customer.Name}.";
        }

        return $"Customer: {customer.Name}\n" +
               $"Account Number: {account.AccountNumber}\n" +
               $"Current Balance: Rs. {account.Balance:N2}";
    }

    /// <summary>
    /// Tool: GetRecentTransactions
    /// Returns the 10 most recent transactions for the customer's account.
    /// </summary>
    private async Task<string> GetRecentTransactionsAsync(int customerId)
    {
        _logger.LogInformation("Executing GetRecentTransactions for customer {CustomerId}", customerId);

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.CustomerId == customerId);

        if (account == null)
        {
            return $"No account found for customer ID {customerId}.";
        }

        // Get the 10 most recent transactions, newest first
        var transactions = await _context.Transactions
            .Where(t => t.AccountId == account.Id)
            .OrderByDescending(t => t.Date)
            .Take(10)
            .ToListAsync();

        if (!transactions.Any())
        {
            return "No transactions found for this account.";
        }

        // Build a simple readable summary
        var lines = new List<string>
        {
            $"Recent Transactions (Account: {account.AccountNumber}):",
            new string('-', 50)
        };

        foreach (var t in transactions)
        {
            lines.Add($"{t.Date:yyyy-MM-dd} | {t.TransactionType,-6} | {t.Category,-15} | Rs. {t.Amount:N2} | {t.Description}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Tool: GetSpendingSummary
    /// Calculates total spending and breaks it down by category.
    /// Only counts "Debit" transactions (money going out).
    /// </summary>
    private async Task<string> GetSpendingSummaryAsync(int customerId)
    {
        _logger.LogInformation("Executing GetSpendingSummary for customer {CustomerId}", customerId);

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.CustomerId == customerId);

        if (account == null)
        {
            return $"No account found for customer ID {customerId}.";
        }

        // Get all debit transactions (money going out = spending)
        var debitTransactions = await _context.Transactions
            .Where(t => t.AccountId == account.Id && t.TransactionType == "Debit")
            .ToListAsync();

        if (!debitTransactions.Any())
        {
            return "No spending transactions found for this account.";
        }

        // Group by category and sum up amounts using LINQ
        // Example result: { "Food": 12000, "Transport": 5000 }
        var spendingByCategory = debitTransactions
            .GroupBy(t => t.Category)
            .Select(group => new
            {
                Category = group.Key,
                Total = group.Sum(t => t.Amount),
                Count = group.Count()
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        var totalSpending = debitTransactions.Sum(t => t.Amount);
        var totalTransactions = debitTransactions.Count;

        // Build the summary text
        var lines = new List<string>
        {
            "Spending Summary:",
            new string('-', 40)
        };

        foreach (var category in spendingByCategory)
        {
            lines.Add($"{category.Category,-20}: Rs. {category.Total:N2} ({category.Count} transactions)");
        }

        lines.Add(new string('-', 40));
        lines.Add($"Total Spending      : Rs. {totalSpending:N2}");
        lines.Add($"Total Transactions  : {totalTransactions}");

        return string.Join("\n", lines);
    }
}
