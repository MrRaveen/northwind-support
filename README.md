# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents, and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## Quick Start & Setup Guide

Follow these commands step-by-step from the repository root to set up, build, test, and run the entire application from start to finish.

### 1. Environment and Dependencies Installation
```bash
# Install all Node dependencies for the monorepo workspace
npm install

# Copy the environment example file to configure local variables
cp .env.example .env

```

### 2. Database Setup (SQL Server via Docker)

```bash
# Start the local SQL Server development container on port 1433
npm run db:up

```

### 3. Running Tests

```bash
# Execute the complete test suite (runs both .NET xUnit backend tests and Vitest frontend tests)
npm run test

```

### 4. Manual Database Migrations & Seeding (Entire Project)

Normally, pending migrations and demo seed data are applied automatically on startup when running in Development mode (`MigrateAndSeedAsync` in `Program.cs`). If you need to apply migrations or update the database schema across the entire project manually, run the following commands:

```bash
# Apply migrations and update the database via EF Core CLI
dotnet ef database update --project apps/api/src/SupportDesk.Infrastructure --startup-project apps/api/src/SupportDesk.Presentation

# To add a brand new migration if modifying model definitions:
npm run ef -- migrations add <MigrationName>

```

### 5. Running the Application

Open two separate terminals to run the backend and frontend concurrently:

* **Terminal 1 (Backend API):**
```bash
npm run api

```


*(Starts the ASP.NET Core API on `https://localhost:7043` / `http://localhost:5043`)*
* **Terminal 2 (Frontend Web Client):**
```bash
npm run web

```


*(Starts the Vite development server on `http://localhost:5173`)*

---

## What Was Implemented

I completed Tasks 1 through 3 of the assignment, focusing on core business logic, automated ticket triage, SLA calculation, and agent authentication.

* **Task 1:** Fixed the UI caching/re-render defect and implemented server-side filtering using EF Core LINQ projections.
* **Task 2:** Automated triage for newly raised and escalated tickets. The SLA window, priority, and agent assignment rules (BR-1 to BR-8) are fully implemented.
* **Task 3:** Implemented agent login using JWT authentication, salted password hashing, and route protection.

---

## Design Decisions

**Domain-Driven Design for Invariants:** I placed the guard rules for ticket transitions (e.g., rejecting escalations for Critical/Closed tickets) directly inside the `Ticket` aggregate root (`NextPriority()` and `Escalate()`). This prevents any use case from bypassing the rules.

**Shared Triage Service:** Rather than duplicating logic between the Create and Escalate command handlers, I extracted the agent capacity matching and SLA window calculations into a single `TicketTriageService`. This ensures that BR-1 through BR-5 are consistently applied regardless of how a ticket enters a new state.

**Authentication Storage:** I added the `PasswordHash` directly to the `Agents` table rather than creating a separate `Users` table. Since an agent maps 1:1 to a user in this bounded context, separating them would only add unnecessary database joins and complicate synchronization.

---

## The Defect (DEFECT-117)

* **Root Cause:** The `useTicketList` hook in the frontend had a missing dependency array. Changes to the dropdowns updated the React state, but the `useEffect` that triggers the API call wasn't watching the non-search filter values.
* **Fix:** Updated the `useEffect` dependency array in `useTicketList.ts` to include all filter parameters (`filters.status`, `filters.priority`, etc.).
* **How to Verify:** Select a new status or priority in the filter bar. The network tab will show an immediate API request without needing to type in the search box.

---

## Filtering

* **Where applied:** In `TicketQueries.cs` using the `ApplyFilters` helper.
* **SQL Check:** Checked against the EF Core SQL output logs in the terminal to ensure `COUNT(*)` and `ORDER BY` happen on the database side (no `IEnumerable` in-memory filtering). SLA filtering was translated into raw SQL `DATEDIFF` calculations.
* **Adding the next filter:** Simply add another `if (query.NewFilter.HasValue)` block inside `ApplyFilters` and append a `.Where()` clause.

---

## Authentication

* **Credential Storage:** `PasswordHash` column on the `Agents` table.
* **Hashing:** Used ASP.NET Core Identity's `PasswordHasher<T>` (PBKDF2 with a random salt per hash). It is abstracted behind an `ICredentialHasher` interface to keep the Domain/Application layers unaware of framework details.


* **Token Issue & Storage:** JWTs are issued by the backend with a 60-minute lifetime. The frontend stores them in `localStorage`.


* **Production Changes:** `localStorage` is vulnerable to XSS. For production, I would change this to issue an `httpOnly` secure cookie for the token, implement anti-CSRF tokens, and move the hardcoded development `SigningKey` to a secure vault like AWS Secrets Manager or Azure Key Vault.



### Development Login Credentials

You can log in to the application using any active seeded agent:

* **Email:** `alex.turner@northwind-support.example` (or `ben.osei`, `sara.lindqvist`, etc.)


* **Password:** `LocalDevOnly!123`


---

## Database Changes

* **Added Tables/Columns:** Added `TicketEscalations` table, `PasswordHash` to `Agents`, and `SlaWindowStartUtc` to `Tickets`.
* **Index:** Added `IX_TicketEscalations_TicketId` on `TicketEscalations`.
* **Why:** The detail page queries the history by Ticket ID (`GET /api/tickets/{id}/escalations`). Without this index, SQL Server would have to perform a full table scan of the escalations table for every ticket load.

---

## Testing

* **How to run:** Execute `npm test` from the repository root. This runs both `dotnet test` and the Vitest UI suites.


* **What was tested:** Complete domain invariants, exact SLA boundary conditions (e.g., exactly at 25%), configuration validation, command handlers, and UI validation states.


* **Deliberately left out:** I did not write deep E2E tests (e.g., Cypress/Playwright) or test the SQLite output for the `DATEDIFF` SLA logic, as SQLite does not support the native SQL Server date functions out of the box.

---

## AI Usage

* **Tools Used:** Used LLM assistants (ChatGPT/Gemini) primarily for generating test boilerplate and creating mock configurations.
* **Workflow:** Prompted the AI with the specific boundary rules (BR-2/BR-8) and asked it to generate parameterized `[Theory]` tests in xUnit.
* **Accepted As-is:** The `SlaOptionsValidationTests` and generic HTTP request boilerplate for the frontend API client.
* **Changed/Rejected:** The AI initially suggested throwing HTTP `ConflictExceptions` inside the `Ticket.cs` domain entity. I rejected this to maintain Clean Architecture principles, ensuring the domain throws `BusinessRuleViolationException` and only the presentation/application layers map that to a 409 status code.


* **Verification:** Verified all generated tests by breaking the production code deliberately to watch the tests fail, and checked the console output to ensure SQL queries were properly formatted.

---

## Known Limitations

* **Docker:** Task 4 (Dockerisation) was left incomplete due to the time budget constraint.
* **Refresh Tokens:** Currently, agents are logged out strictly after 60 minutes and must re-authenticate. Refresh token rotation is not implemented.


* **NuGet Vulnerabilities:** Warning NU1902/NU1903 regarding `System.Security.Cryptography.Pkcs` is present in the build output due to transitive dependencies; upgrading the SDK/Identity packages is required to resolve it permanently.
