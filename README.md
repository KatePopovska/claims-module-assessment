# Claims Module Assessment

Vertical slice of a Claims Management System (FNOL intake + Reserve management) — see `docs/` for the full requirements extraction, business rules, API contract, architecture decisions, and progress log.

## Repository Layout

```
docs/               Requirements, domain model, business rules, API contract, architecture, decisions, progress
docs/source/         Original DOCX specifications (FRS + Assessment Brief)
backend/             .NET 9 Clean Architecture solution
  src/ClaimsModule.Domain
  src/ClaimsModule.Application
  src/ClaimsModule.Infrastructure
  src/ClaimsModule.Persistence
  src/ClaimsModule.API
frontend/            Angular 19 workspace
```

## Prerequisites

- .NET 9 SDK
- Node.js 20+ and npm
- SQL Server (LocalDB, Docker, or Azure SQL) — only needed once you run migrations; the backend builds and serves Swagger without one

## Backend

```
cd backend
dotnet build
dotnet run --project src/ClaimsModule.API
```

Swagger UI: `https://localhost:{port}/swagger` (Development environment only). Health check: `/api/health`.

Configuration (`backend/src/ClaimsModule.API/appsettings.Development.json`):
- `ConnectionStrings:DefaultConnection` — SQL Server connection string (defaults to LocalDB)
- `Storage:Provider` — `LocalFileSystem` (default) or `AzureBlob`
- `OrganisationId` — fixed tenant GUID seeded across the app (FRS §15.1)

## Database

Apply the schema (also seeds reference data via EF `HasData`):

```
cd backend
dotnet ef database update --project src/ClaimsModule.Persistence --startup-project src/ClaimsModule.API
```

Standalone seed scripts for reference data (`backend/database/seed/`) — idempotent, safe to re-run against a migrated database, same rows as the EF seed:

```
sqlcmd -S <server> -d <database> -i backend/database/seed/01-cause-of-loss-codes.sql
sqlcmd -S <server> -d <database> -i backend/database/seed/02-policies.sql
```

## Frontend

```
cd frontend
npm install
npm start
```

Runs at `http://localhost:4200`, proxying API calls to `http://localhost:5288/api` (see `src/environments/environment.development.ts`).

## Status

Solution and workspace scaffolding only — no domain entities, migrations, or feature endpoints yet. See `docs/progress.md` for what's done and what's next.
