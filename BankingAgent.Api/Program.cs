using BankingAgent.Api.Data;
using BankingAgent.Api.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

// ─────────────────────────────────────────────────────────────────────────────
// Program.cs - The starting point of the application
//
// This file does two main things:
//   1. REGISTER SERVICES  - Tell ASP.NET Core what services/dependencies exist
//   2. CONFIGURE PIPELINE - Tell ASP.NET Core how to process HTTP requests
//
// Think of it like setting up the kitchen before opening a restaurant.
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════════
// SECTION 1: Register Services (Dependency Injection Container)
//
// "Dependency Injection" means: instead of creating objects yourself (new Foo()),
// you tell ASP.NET Core how to create them, and it will pass them to your classes
// automatically via the constructor.
//
// Benefits:
//   - Classes don't know about each other's internals
//   - Easy to swap implementations (e.g., for testing)
//   - Lifetime is managed for you (you won't accidentally create too many DbContexts)
// ═══════════════════════════════════════════════════════════════════════════

// ── Controllers ────────────────────────────────────────────────────────────
// Registers all classes that inherit from ControllerBase as controllers.
// ASP.NET Core will route HTTP requests to the correct controller automatically.
builder.Services.AddControllers();

// ── OpenAPI / Scalar ────────────────────────────────────────────────────────
// .NET 10 has built-in OpenAPI support via Microsoft.AspNetCore.OpenApi.
// Scalar provides a beautiful interactive API testing UI (like Swagger UI).
// Access it at: http://localhost:PORT/scalar/v1
builder.Services.AddOpenApi();

// ── Entity Framework Core + SQL Server ────────────────────────────────────
// Registers AppDbContext with the DI container.
//
// When a controller or service needs AppDbContext, ASP.NET Core will:
//   1. Create a new AppDbContext instance
//   2. Connect it to SQL Server using the connection string
//   3. Inject it into the constructor automatically
//
// The connection string comes from appsettings.json → ConnectionStrings:DefaultConnection
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── HttpClient for OpenRouterService ──────────────────────────────────────
// HttpClient is the .NET class we use to make HTTP requests to external APIs.
//
// AddHttpClient<OpenRouterService>() tells ASP.NET Core two things:
//   1. When someone needs OpenRouterService, create one and inject it
//   2. Also create an HttpClient and inject it into OpenRouterService's constructor
//
// Why not just "new HttpClient()" inside OpenRouterService?
// Because creating HttpClient manually can cause "socket exhaustion" (running out of
// network connections). AddHttpClient() uses a pool of connections safely.
builder.Services.AddHttpClient<OpenRouterService>();

// ── AgentService ───────────────────────────────────────────────────────────
// Registers AgentService so it can be injected into AgentController.
// AddScoped means: create one instance per HTTP request (good default for services).
builder.Services.AddScoped<AgentService>();

// ── CORS (Cross-Origin Resource Sharing) ──────────────────────────────────
// Browsers block JavaScript from calling APIs on different domains by default.
// Our Angular app runs on http://localhost:4200, our API on http://localhost:5XXX.
// These are different "origins", so we must explicitly allow it.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")  // Angular dev server
              .AllowAnyHeader()                      // Allow any HTTP headers
              .AllowAnyMethod();                     // Allow GET, POST, PUT, DELETE, etc.
    });
});

// ─────────────────────────────────────────────────────────────────────────────
// SECTION 2: Build the application
// After registering all services, we build the app and configure the HTTP pipeline.
// ─────────────────────────────────────────────────────────────────────────────

var app = builder.Build();

// ═══════════════════════════════════════════════════════════════════════════
// SECTION 3: Configure the HTTP Request Pipeline (Middleware)
//
// Middleware processes each HTTP request in order, like a chain.
// Each "Use..." call adds a step in that chain.
//
// Request flow:
//   HTTP Request → OpenAPI → CORS → Controllers → HTTP Response
// ═══════════════════════════════════════════════════════════════════════════

// ── OpenAPI + Scalar UI ────────────────────────────────────────────────────
// Only show API docs in development, not in production
if (app.Environment.IsDevelopment())
{
    // Serves the OpenAPI JSON spec at /openapi/v1.json
    app.MapOpenApi();

    // Scalar UI: a beautiful, modern API testing interface
    // Available at: http://localhost:PORT/scalar/v1
    app.MapScalarApiReference();
}

// ── HTTPS Redirection ──────────────────────────────────────────────────────
// Redirects http:// requests to https:// automatically
app.UseHttpsRedirection();

// ── CORS ───────────────────────────────────────────────────────────────────
// Apply the CORS policy we configured above.
// This MUST come before app.MapControllers()
app.UseCors("AllowAngular");

// ── Authorization ──────────────────────────────────────────────────────────
// Needed for [Authorize] attributes. We're not using auth yet, but it's good practice.
app.UseAuthorization();

// ── Controllers ────────────────────────────────────────────────────────────
// Routes incoming HTTP requests to the correct controller and action method.
// For example: POST /api/agent → AgentController.Chat()
app.MapControllers();

// ── Start the application ──────────────────────────────────────────────────
app.Run();
