# EXPLANATION.md — Banking Agent API

This document explains every important part of the project in plain language,
aimed at a beginner .NET developer.

---

## 1. Project Architecture

```
Angular (Frontend - not built yet)
         │
         │  HTTP POST /api/agent
         ▼
AgentController  ← receives the HTTP request
         │
         │  calls ChatAsync(customerId, message)
         ▼
AgentService  ← coordinates the AI + tools
         │
    ┌────┴────────────────────┐
    │                         │
    ▼                         ▼
OpenRouter / Nemotron      AppDbContext (EF Core)
(AI model)                 (SQL Server database)
```

**The flow in plain English:**

1. Angular calls our API with a customer ID and their message.
2. `AgentController` receives the request and passes it to `AgentService`.
3. `AgentService` sends the message to the Nemotron AI on OpenRouter.
4. The AI decides if it needs real banking data (like the balance).
5. If it does, it sends back a "tool call" request — e.g., "run GetBalance(1)".
6. We run our C# `GetBalanceAsync()` function which queries SQL Server.
7. We send the result back to the AI.
8. The AI writes a natural-language response using that real data.
9. We return the final response to Angular.

---

## 2. Program.cs — What Every Section Does

`Program.cs` is the entry point. It has two jobs:

### Part 1 — Register Services

```csharp
builder.Services.AddControllers();
```
This tells ASP.NET Core about all our controllers (AgentController, CustomersController, etc.).
When an HTTP request arrives, ASP.NET Core knows which controller handles which URL.

```csharp
builder.Services.AddOpenApi();
```
Enables automatic API documentation. You can see and test all your endpoints at `/scalar/v1`.

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(...));
```
Registers our database context. Every time a controller or service asks for an `AppDbContext`,
ASP.NET Core creates one connected to our SQL Server database.

```csharp
builder.Services.AddHttpClient<OpenRouterService>();
```
Registers `OpenRouterService` AND creates a properly managed `HttpClient` for it.
This is the safe way to use HttpClient. See section 7 for more detail.

```csharp
builder.Services.AddScoped<AgentService>();
```
Registers `AgentService`. `Scoped` means: create one new instance for each incoming HTTP request.

```csharp
builder.Services.AddCors(...)
```
Allows our Angular app (at `localhost:4200`) to call our API (at a different port).
Without CORS, browsers would block the request.

### Part 2 — Build the HTTP Pipeline

After `var app = builder.Build();`, we configure how each request flows:

```csharp
app.MapOpenApi();          // Serve the OpenAPI JSON spec
app.MapScalarApiReference(); // Serve the Scalar UI for testing
app.UseHttpsRedirection(); // Auto-redirect http → https
app.UseCors("AllowAngular"); // Apply the CORS rules
app.UseAuthorization();    // Apply [Authorize] attributes
app.MapControllers();      // Route requests to controllers
app.Run();                 // Start listening for requests
```

---

## 3. Dependency Injection — Why We Register Services

**Dependency Injection (DI)** is a pattern where objects don't create their own dependencies.
Instead, they receive them via the constructor.

**Without DI (bad way):**
```csharp
public class AgentController
{
    public AgentController()
    {
        // You'd have to create everything manually - messy and hard to test
        var config = new ConfigurationBuilder()...Build();
        var httpClient = new HttpClient(); // DANGEROUS - causes socket exhaustion
        var openRouter = new OpenRouterService(httpClient, config, ...);
        var dbContext = new AppDbContext(...);
        var agentService = new AgentService(openRouter, dbContext, ...);
    }
}
```

**With DI (correct way):**
```csharp
public class AgentController
{
    public AgentController(AgentService agentService, ILogger<AgentController> logger)
    {
        // ASP.NET Core creates and provides these for us automatically
        _agentService = agentService;
        _logger = logger;
    }
}
```

We register services in `Program.cs`:
```csharp
builder.Services.AddHttpClient<OpenRouterService>(); // Registers OpenRouterService
builder.Services.AddScoped<AgentService>();           // Registers AgentService
```

Then ASP.NET Core wires everything together automatically.

**Why `AddHttpClient<OpenRouterService>()`?**
- Creates `OpenRouterService` and injects an `HttpClient` into it.
- The `HttpClient` is managed by a connection pool, preventing socket exhaustion.
- You should never do `new HttpClient()` in a service class.

---

## 4. DbContext — The Bridge to SQL Server

### What is DbContext?

`AppDbContext` is our database session. It's like a "shopping basket" for database operations —
you add/remove/query things, and when you're done, EF Core figures out the SQL to run.

```csharp
public class AppDbContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
}
```

### What is DbSet?

`DbSet<T>` represents a **table** in your database.
- `Customers` → the `Customers` table
- `Accounts` → the `Accounts` table
- `Transactions` → the `Transactions` table

You query DbSet like a regular C# collection:
```csharp
// EF Core translates this to:  SELECT * FROM Accounts WHERE CustomerId = 1
var account = await _context.Accounts
    .FirstOrDefaultAsync(a => a.CustomerId == customerId);
```

### How EF Core Communicates with SQL Server

1. You write C# LINQ queries against DbSet properties.
2. EF Core translates your LINQ into SQL at runtime.
3. EF Core sends the SQL to SQL Server using the connection string.
4. SQL Server returns rows.
5. EF Core maps the rows back into your C# classes (Customer, Account, Transaction).

You never write raw SQL. You work with C# objects.

---

## 5. Models — The Entities

Models are plain C# classes that represent rows in database tables.

### Customer
```csharp
public class Customer
{
    public int Id { get; set; }      // Primary key (auto-generated by SQL)
    public string Name { get; set; }
    public string Email { get; set; }
    public List<Account> Accounts { get; set; } // Navigation: customer's accounts
}
```

### Account
```csharp
public class Account
{
    public int Id { get; set; }
    public int CustomerId { get; set; }      // Foreign key linking to Customer
    public string AccountNumber { get; set; }
    public decimal Balance { get; set; }
    public Customer Customer { get; set; }   // Navigation: the owner
    public List<Transaction> Transactions { get; set; }
}
```

### Transaction
```csharp
public class Transaction
{
    public int Id { get; set; }
    public int AccountId { get; set; }       // Foreign key linking to Account
    public decimal Amount { get; set; }      // Always positive
    public string Category { get; set; }     // "Food", "Transport", etc.
    public string Description { get; set; }
    public DateTime Date { get; set; }
    public string TransactionType { get; set; } // "Debit" or "Credit"
    public Account Account { get; set; }     // Navigation: the account
}
```

**Navigation Properties**: These are the `Account Account { get; set; }` type properties.
They don't exist as columns in the database. EF Core uses them to automatically load
related objects. For example, `account.Transactions` loads all transactions for that account.

---

## 6. Controllers — Handling HTTP Requests

### What is a Controller?

A controller is a class that handles HTTP requests. Each public method in the controller
is called an "action" and maps to one endpoint (URL).

### Key Attributes Explained

```csharp
[ApiController]           // Tells ASP.NET Core this is an API controller
[Route("api/agent")]      // All endpoints start with /api/agent
public class AgentController : ControllerBase
```

- `[ApiController]` — Enables automatic model validation, automatic 400 responses for bad input,
  and other API-friendly behaviors.
- `[Route("api/agent")]` — The base URL path for this controller.
- `ControllerBase` — The base class for API controllers (use `Controller` only for MVC views).

```csharp
[HttpPost]   // This method responds to: POST /api/agent
public async Task<IActionResult> Chat([FromBody] AgentRequest request)
```

- `[HttpPost]` — This action only responds to HTTP POST requests.
- `[FromBody]` — ASP.NET Core reads the JSON request body and deserializes it into `AgentRequest`.
- `IActionResult` — The return type. Lets us return `Ok(...)`, `NotFound(...)`, `BadRequest(...)` etc.

### The Controller Rule

**Controllers should be thin.** They should only:
1. Receive the request
2. Validate basic inputs
3. Call a service
4. Return the result

All business logic lives in services, not controllers.

---

## 7. OpenRouterService — Calling the AI

### What is HttpClient?

`HttpClient` is a .NET class for making HTTP requests to external APIs (like OpenRouter's API).
It works like a browser — you send a request, you get a response.

```csharp
var response = await _httpClient.SendAsync(request);
var body = await response.Content.ReadAsStringAsync();
```

### Why is the API Key NOT in Source Code?

The API key is a secret. If you put it in a `.cs` file:
- Anyone who sees the source code (on GitHub, in a code review, etc.) can steal it.
- They could use your API key and run up a large bill on your account.

Instead, we use **.NET User Secrets**:
```bash
dotnet user-secrets set "OpenRouter:ApiKey" "your-actual-key"
```

This stores the key in a private file on your computer (NOT in the project folder):
`%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`

The key is read at runtime:
```csharp
var apiKey = _configuration["OpenRouter:ApiKey"];
```

User Secrets are only used during development. In production/Azure, you'd use
environment variables or Azure Key Vault.

### What Request Do We Send?

We send a JSON body to `https://openrouter.ai/api/v1/chat/completions`:

```json
{
  "model": "nvidia/nemotron-3-super-120b-a12b:free",
  "messages": [
    { "role": "system", "content": "You are a banking assistant..." },
    { "role": "user", "content": "What is my balance?" }
  ],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "GetBalance",
        "description": "Get the balance for the customer",
        "parameters": { ... }
      }
    }
  ],
  "tool_choice": "auto"
}
```

### What Response Do We Get?

If the AI answers directly:
```json
{
  "choices": [{
    "finish_reason": "stop",
    "message": {
      "role": "assistant",
      "content": "Your balance is Rs. 185,000."
    }
  }]
}
```

If the AI wants to call a tool first:
```json
{
  "choices": [{
    "finish_reason": "tool_calls",
    "message": {
      "role": "assistant",
      "tool_calls": [{
        "id": "call_abc123",
        "function": {
          "name": "GetBalance",
          "arguments": "{\"customerId\": 1}"
        }
      }]
    }
  }]
}
```

---

## 8. AgentService — The AI Agent Loop

### What Makes This an Agent?

A simple chatbot just sends a message and gets text back.

An **AI agent** can:
- Decide if it needs additional information
- Request real functions to be run (tools)
- Use the results to answer accurately

### What is Tool Calling?

We provide the AI with a list of "tools" — descriptions of functions it can request.
The AI reads these descriptions and decides which one to use.

```
Tools we define:
  - GetBalance:            "Get the customer's account balance"
  - GetRecentTransactions: "Get the customer's recent transactions"
  - GetSpendingSummary:    "Get a breakdown of spending by category"
```

The AI can't run these functions itself. It just says "I want you to run GetBalance".
We run the actual C# code, then give the result back to the AI.

### Why Can't the AI Access SQL Directly?

This is a critical security principle. If we let the AI run any SQL:
- It could delete data: `DROP TABLE Customers`
- It could read other customers' data
- It could be tricked (prompt injection) into running harmful queries

By only allowing predefined tools, we stay in control. The AI can only ask for
`GetBalance`, `GetRecentTransactions`, or `GetSpendingSummary`. Nothing else.

### The Tool-Calling Loop Step by Step

```
1. Build conversation:
   messages = [
     { role: "system", content: "You are a banking assistant. Customer ID is 1." },
     { role: "user",   content: "What is my balance?" }
   ]

2. Send to Nemotron with tool definitions.

3. Nemotron replies with finish_reason = "tool_calls":
   { role: "assistant", tool_calls: [{ name: "GetBalance", arguments: '{"customerId":1}' }] }

4. We add the AI's tool_call message to the conversation history.

5. We run GetBalanceAsync(1):
   → Queries SQL Server
   → Returns "Balance: Rs. 185,000"

6. We add the tool result to conversation:
   { role: "tool", tool_call_id: "call_abc123", content: "Balance: Rs. 185,000" }

7. Send updated conversation back to Nemotron (no tools this time).

8. Nemotron reads the tool result and responds:
   "Your current account balance is Rs. 185,000."

9. Return that final text to the controller.
```

---

## 9. Complete Data Flow — Example

```
User types: "What is my balance?"
              │
              ▼
          Angular
          sends: POST http://localhost:5XXX/api/agent
          body: { "customerId": 1, "message": "What is my balance?" }
              │
              ▼
      AgentController.Chat()
      receives the AgentRequest object
      calls AgentService.ChatAsync(1, "What is my balance?")
              │
              ▼
        AgentService
        builds the messages list:
          - system: "You are a banking assistant. Customer ID is 1."
          - user: "What is my balance?"
        sends to OpenRouterService with tool definitions
              │
              ▼
        OpenRouterService
        adds API key to Authorization header
        sends HTTP POST to https://openrouter.ai/api/v1/chat/completions
              │
              ▼
        Nemotron AI
        reads the question + tool descriptions
        decides: "I need GetBalance"
        responds with finish_reason = "tool_calls"
              │
              ▼
        AgentService
        sees finish_reason = "tool_calls"
        extracts tool name: "GetBalance"
        calls ExecuteToolAsync() → GetBalanceAsync(1)
              │
              ▼
          AppDbContext (EF Core)
          runs: SELECT * FROM Accounts WHERE CustomerId = 1
              │
              ▼
          SQL Server
          returns row: AccountNumber="PK-0001", Balance=185000
              │
              ▼
        AgentService
        formats result: "Balance: Rs. 185,000.00"
        adds tool result to conversation history
        sends updated conversation to Nemotron (second call)
              │
              ▼
        Nemotron AI
        reads the tool result
        writes: "Your current account balance is Rs. 185,000."
              │
              ▼
        AgentService
        returns the final text
              │
              ▼
      AgentController
      returns: HTTP 200 OK
      body: { "response": "Your current account balance is Rs. 185,000." }
              │
              ▼
          Angular
          displays the response to the user
```

---

## 10. NuGet Packages — What Each One Does

| Package | Purpose |
|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer` | EF Core provider for SQL Server. Translates LINQ queries into T-SQL. |
| `Microsoft.EntityFrameworkCore.Design` | Needed for the `dotnet ef migrations add` command to work. |
| `Microsoft.EntityFrameworkCore.Tools` | Provides the `dotnet ef` CLI commands. |
| `Microsoft.AspNetCore.OpenApi` | Built-in .NET 10 package. Generates OpenAPI JSON spec automatically. |
| `Scalar.AspNetCore` | Provides the `/scalar/v1` interactive API testing UI (modern alternative to Swagger UI). |

---

## 11. How to Run the Project

### Step 1: Set the API Key
```bash
cd BankingAgent.Api
dotnet user-secrets set "OpenRouter:ApiKey" "YOUR_OPENROUTER_KEY_HERE"
```
Get a free key at: https://openrouter.ai

### Step 2: Create the Database
The migration has already been created. Just run:
```bash
dotnet ef database update
```
This creates the `BookDb` database in your SQL Server with the correct tables.

### Step 3: Add Demo Data
Run the SQL INSERT statements from `TASK.md` in SQL Server Management Studio.

### Step 4: Run the Application
```bash
dotnet run
```

### Step 5: Test the API
Open the Scalar UI in your browser:
```
http://localhost:PORT/scalar/v1
```
(The actual port is shown in the console when you run `dotnet run`)

Test the agent:
- Click `POST /api/agent`
- Enter: `{ "customerId": 1, "message": "What is my balance?" }`
- Click Execute

---

## 12. Common Mistakes and How to Avoid Them

| Mistake | Why It's Wrong | Correct Approach |
|---|---|---|
| Putting the API key in `appsettings.json` | Anyone with access to the file can steal it | Use User Secrets |
| Using `new HttpClient()` in a class | Causes socket exhaustion | Use `AddHttpClient<T>()` in DI |
| Putting business logic in Controllers | Hard to test, hard to maintain | Put logic in Services |
| Letting the AI run arbitrary SQL | Security risk | Only expose predefined tool functions |
| Using `new DbContext()` manually | Incorrect lifetime management | Register with `AddDbContext()` |

---

## 13. File Structure Reference

```
BankingAgent.Api/
│
├── Controllers/
│   ├── AgentController.cs        ← POST /api/agent (main AI endpoint)
│   ├── CustomersController.cs    ← GET /api/customers (testing)
│   ├── AccountsController.cs     ← GET /api/accounts/{id} (testing)
│   └── TransactionsController.cs ← GET /api/transactions/{id} (testing)
│
├── Data/
│   ├── AppDbContext.cs            ← Database session / EF Core context
│   └── AppDbContextFactory.cs    ← Helps EF migrations work (CLI only)
│
├── Migrations/
│   └── ...                        ← Auto-generated by "dotnet ef migrations add"
│
├── Models/
│   ├── Customer.cs               ← Customers table
│   ├── Account.cs                ← Accounts table
│   └── Transaction.cs            ← Transactions table
│
├── Services/
│   ├── OpenRouterService.cs      ← Communicates with OpenRouter / Nemotron
│   └── AgentService.cs           ← AI agent loop + tool execution
│
├── Program.cs                    ← App startup, DI registration, HTTP pipeline
├── appsettings.json              ← Connection string (no secrets here!)
├── TASK.md                       ← Project progress checklist
└── EXPLANATION.md                ← This file
```

---

## 14. Endpoint Testing & Bug Fixes

### Test Results Summary

All endpoints have been tested against live SQL Server data and verified working:

| Endpoint | Method | Query / Payload | Result |
|---|---|---|---|
| `/api/customers` | `GET` | None | Returns all 5 seeded customers (Ali Khan, Sara Ahmed, etc.) |
| `/api/customers/1` | `GET` | Id: 1 | Returns customer details for Ali Khan (HTTP 200) |
| `/api/customers/999`| `GET` | Id: 999 | Returns HTTP 404 Not Found |
| `/api/accounts/1` | `GET` | CustomerId: 1 | Returns account `PK-DEMO-10001` with balance `Rs. 185,000.00` |
| `/api/transactions/1`| `GET`| CustomerId: 1 | Returns 4 transactions (Food, Utilities, Transport, Salary) |
| `/api/agent` | `POST` | "What is my balance?" | AI calls `GetBalance` → returns "Your current balance is **Rs. 185,000.00**." |
| `/api/agent` | `POST` | "Show me my recent transactions" | AI calls `GetRecentTransactions` → formats neat Markdown table with 4 transactions |
| `/api/agent` | `POST` | "How much did I spend this month..." | AI calls `GetSpendingSummary` → breaks down Rs. 15,200 across Utilities, Transport, Food |
| `/api/agent` | `POST` | "How much did I spend on food?" | AI calls `GetSpendingSummary` → answers "You’ve spent **Rs. 2,500.00** on food." |
| `/api/agent` | `POST` | CustomerId: 0 | Returns HTTP 400 Bad Request (`"CustomerId must be a positive number."`) |
| `/api/agent` | `POST` | Message: " " | Returns HTTP 400 Bad Request (`"Message cannot be empty."`) |
| `/api/agent` | `POST` | CustomerId: 999 | Returns polite response stating customer was not found (see bug fix below) |

---

### Bug Discovered & Resolution

#### The Bug
When querying `/api/agent` with a non-existent customer ID (e.g. `customerId: 999`), the model previously returned raw XML tags in the JSON response:
```json
{
  "response": "<tool_call>\n<function=getCustomerInfo>\n<parameter=customerId>\n999\n</parameter>\n</function>\n</tool_call>\n"
}
```

#### Why This Happened
1. When `customerId = 999` was provided, the AI properly called `GetBalance(999)`.
2. Our C# method returned: `"Customer with ID 999 was not found."`
3. We sent this tool result back to Nemotron in the second call without tool definitions (since tools had already executed).
4. Seeing that the customer was not found and having no active tool definitions in the second call, the Nemotron model hallucinated an unannounced tool (`getCustomerInfo`) and outputted raw XML syntax directly into `Message.Content`.

#### How We Fixed It
1. **Explicit Prompting**: Updated the system prompt in [`Services/AgentService.cs`](file:///c:/Users/ZAIN%20BAIG/Desktop/agentapi/BankingAgent.Api/Services/AgentService.cs) to instruct the model:
   > *"If a tool reports that the customer or account is not found, state that politely in plain text. Never output XML, raw tool tags, or call nonexistent tools."*
2. **Safety Sanitizer**: Added fallback handling in [`Services/AgentService.cs`](file:///c:/Users/ZAIN%20BAIG/Desktop/agentapi/BankingAgent.Api/Services/AgentService.cs). If the model's text response ever contains raw `<tool_call>` tags or empty content, it returns a clean, user-friendly message:
   `"Customer with ID {customerId} was not found. Please verify your customer ID."`
3. **Verification**: Re-testing `customerId: 999` now yields:
   `"I’m sorry, but I couldn’t locate an account for customer ID 999. Please double‑check the ID or contact support for further assistance."`

