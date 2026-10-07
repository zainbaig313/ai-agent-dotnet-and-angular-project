# Project Tasks — Banking Agent API

> Track progress here. Check off items as they are completed.

---

## ✅ Project Setup

- [x] Create ASP.NET Core Web API project
- [x] Install EF Core packages (`Microsoft.EntityFrameworkCore.SqlServer`, `Design`, `Tools`)
- [x] Install Scalar.AspNetCore (modern API documentation UI)
- [x] Configure SQL Server connection string in `appsettings.json`
- [x] Create `AppDbContext`
- [x] Create entities: `Customer`, `Account`, `Transaction`
- [x] Create `AppDbContextFactory` (needed for EF migrations)
- [x] Create initial EF migration (`InitialCreate`)
- [x] Apply migration → database tables created in SQL Server

---

## ✅ OpenRouter / AI Configuration

- [x] Initialize .NET User Secrets (`dotnet user-secrets init`)
- [x] Read API key via `_configuration["OpenRouter:ApiKey"]`
- [x] API key is NOT in any source file
- [x] Set your actual API key (configured in user-secrets)
- [x] Create `OpenRouterService.cs`
- [x] Configure request to use `nvidia/nemotron-3-super-120b-a12b:free`
- [x] Configure request to send tool definitions
- [x] Parse tool call response from AI
- [x] Test basic OpenRouter connection (verified working)

---

## ✅ Agent

- [x] Create `AgentService.cs`
- [x] Implement tool-calling loop (first call → tool execution → second call)
- [x] Add `GetBalance` tool
- [x] Add `GetRecentTransactions` tool
- [x] Add `GetSpendingSummary` tool
- [x] Connect all tools to EF Core / SQL Server
- [x] System prompt defines AI role and customer context

---

## ✅ API Controllers

- [x] Create `AgentController` → `POST /api/agent`
- [x] Create `CustomersController` → `GET /api/customers`, `GET /api/customers/{id}`
- [x] Create `AccountsController` → `GET /api/accounts/{customerId}`
- [x] Create `TransactionsController` → `GET /api/transactions/{customerId}`
- [x] Configure CORS for Angular (`http://localhost:4200`)

---

## ✅ Testing (Completed & Verified)

- [x] Add demo data using SQL INSERT statements
- [x] Test `GET /api/customers` returns customer list
- [x] Test `GET /api/accounts/1` returns account and balance
- [x] Test `GET /api/transactions/1` returns transactions
- [x] Test `POST /api/agent` with "What is my balance?"
- [x] Test `POST /api/agent` with "Show me my recent transactions"
- [x] Test `POST /api/agent` with "How much did I spend this month?"
- [x] Test `POST /api/agent` with "How much did I spend on food?"
- [x] Test edge case: customer does not exist (handled gracefully)
- [x] Test edge case: input validation errors (400 Bad Request)

---

## 🔲 Future Work (Not in V1)

- [ ] Build Angular 20 frontend
- [ ] Add authentication (JWT or ASP.NET Core Identity)
- [ ] Add per-customer session/conversation history
- [ ] Add more tools (transfer money, pay bills, etc.)
- [ ] Add support for multiple accounts per customer
- [ ] Add real-time notifications (SignalR)
- [ ] Deploy to Azure or similar cloud

---


```
