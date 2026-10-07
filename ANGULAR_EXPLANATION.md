# Angular Frontend Explanation — Beginner's Guide

This guide explains how the **Angular 20 Frontend** is built, how each file functions, and how it communicates with the **ASP.NET Core Web API** backend.

---

## 1. Project Directory Structure

```text
banking-ui/
├── src/
│   ├── index.html                   # Base HTML document with page title and root element
│   ├── styles.css                   # Global styles (body font, background, margin)
│   ├── environments/
│   │   └── environment.ts           # Central place for backend API base URL
│   └── app/
│       ├── app.config.ts            # Application providers (ZoneJS, HttpClient)
│       ├── app.ts                   # Root component definition
│       ├── app.html                 # Root component template (renders Dashboard)
│       ├── app.css                  # Root component styling
│       ├── models.ts                # TypeScript interfaces (Customer, Account, Transaction, ChatMessage)
│       ├── services/
│       │   ├── customer.service.ts   # Calls /api/customers endpoints
│       │   ├── account.service.ts    # Calls /api/accounts/{customerId}
│       │   ├── transaction.service.ts# Calls /api/transactions/{customerId}
│       │   └── agent.service.ts      # Calls POST /api/agent for AI inquiries
│       └── components/
│           └── dashboard/
│               ├── dashboard.component.ts    # Dashboard logic & state
│               ├── dashboard.component.html  # HTML structure (tables, inputs, forms)
│               └── dashboard.component.css   # Clean, traditional CSS styling
```

---

## 2. Core Files and Components

### `app.config.ts`
- **What it does**: In modern Angular, `app.config.ts` configures application-wide dependencies.
- **Key feature**: It registers `provideHttpClient()`. Without this, Angular would not be able to inject `HttpClient` into services to make network requests.

### `app.ts` & `app.html`
- **What it does**: The entry root component (`<app-root>`).
- It imports `DashboardComponent` and displays it using `<app-dashboard></app-dashboard>`.

### `models.ts`
- **What it does**: Defines TypeScript interfaces matching the data shapes returned by the backend:
  - `Customer`: `{ id, name, email }`
  - `Account`: `{ id, accountNumber, balance, customerId }`
  - `Transaction`: `{ id, amount, category, description, date, transactionType }`
  - `AgentRequest`: `{ customerId, message }`
  - `AgentResponse`: `{ response }`
  - `ChatMessage`: `{ sender: 'user' | 'ai', text: string }`

---

## 3. Angular Services (`src/app/services/`)

Angular services handle all HTTP communication with the backend. Components do not call URLs directly; they ask services for data.

1. **`CustomerService` (`customer.service.ts`)**:
   - `getCustomers()`: Sends `GET http://localhost:5146/api/customers`. Returns an array of customers.
   - `getCustomer(id)`: Sends `GET http://localhost:5146/api/customers/{id}`. Returns single customer details.

2. **`AccountService` (`account.service.ts`)**:
   - `getAccounts(customerId)`: Sends `GET http://localhost:5146/api/accounts/{customerId}`. Returns the account information (number and current balance).

3. **`TransactionService` (`transaction.service.ts`)**:
   - `getTransactions(customerId)`: Sends `GET http://localhost:5146/api/transactions/{customerId}`. Returns the customer's 20 most recent transactions.

4. **`AgentService` (`agent.service.ts`)**:
   - `askAgent(customerId, message)`: Sends `POST http://localhost:5146/api/agent` with `{ customerId, message }`. Returns `{ response: string }`.

---

## 4. How `DashboardComponent` Works

The dashboard component manages all the UI state and user interactions:

1. **Initial Page Load (`ngOnInit`)**:
   - Calls `loadCustomers()`.
   - Populates the `customers` array.
   - Automatically selects the first customer (e.g., Ali Khan) and triggers `onCustomerChange()`.

2. **Selecting a Customer (`onCustomerChange`)**:
   - When the user selects a customer from the `<select>` dropdown:
     - Fetches customer details via `CustomerService`.
     - Fetches account balance via `AccountService`.
     - Fetches transaction list via `TransactionService`.
   - The UI updates automatically via Angular's template binding.

3. **Asking the AI Assistant (`askAI`)**:
   - Validates that the input message is not empty.
   - Appends `{ sender: 'user', text: userMessage }` to the `messages` array.
   - Displays a clean `AI is thinking...` state and disables the button.
   - Sends the question along with `selectedCustomerId` to `AgentService`.
   - Upon receiving the reply, appends `{ sender: 'ai', text: res.response }` to the chat list.

---

## 5. How Angular Connects to the Backend (CORS & Base URL)

- **Environment Config**: The base URL `http://localhost:5146` is configured in `src/environments/environment.ts`. Services import this single constant.
- **CORS (Cross-Origin Resource Sharing)**: Because the Angular frontend runs on port `4200` and the ASP.NET Core backend runs on port `5146`, the ASP.NET Core backend includes an `AllowAngular` policy in `Program.cs` allowing requests from `http://localhost:4200`.

---

## 6. Complete Request Flow Diagram

When a user asks: *"What is my balance?"*:

```text
User types "What is my balance?" and clicks "Ask AI"
   │
   ▼
Angular Dashboard Component (dashboard.component.ts)
   │
   ▼
Angular AgentService (agent.service.ts)
   │
   ▼
HTTP POST Request (http://localhost:5146/api/agent)
   │
   ▼
ASP.NET Core AgentController (AgentController.cs)
   │
   ▼
AgentService (AgentService.cs)
   │
   ▼
OpenRouter API (Nvidia Nemotron 3 AI model)
   │
   ▼
AI determines it needs balance data → calls GetBalance Tool
   │
   ▼
EF Core / SQL Server database query
   │
   ▼
AI formulates final response: "Your current balance is Rs. 185,000.00."
   │
   ▼
ASP.NET Core returns JSON: { "response": "Your current balance is Rs. 185,000.00." }
   │
   ▼
Angular AgentService receives Observable response
   │
   ▼
Dashboard Component pushes response to chat messages list
   │
   ▼
HTML updates and User sees AI response on screen!
```
