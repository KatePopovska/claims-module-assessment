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
frontend/            Angular 21 application (Angular Material, standalone components, signals)
```

## Prerequisites

- .NET 9 SDK
- Node.js 24.13.0 with its bundled npm 11.6.2 (pinned in `frontend/.nvmrc`; CI uses the same version)
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
npm ci
npm start
```

Runs at `http://localhost:4200` and calls the API at `http://localhost:5160/api` (`src/environments/environment.development.ts`); start the backend first. The production build (`npm run build`) targets the Azure API URL in `src/environments/environment.ts`.

| Command | Purpose |
|---|---|
| `npm start` | Dev server with live reload |
| `npm run build` | Production build into `dist/claims-module-web/browser` |
| `npx ng test --watch=false` | Unit tests (Vitest) |

**Mock sign-in.** There is no login screen: the user menu in the top-right corner switches between three mock users. The selection is remembered in the browser, and every API request carries a Bearer token with that user's id and role.

| User | Role | User id | Can |
|---|---|---|---|
| Hannah Reed | handler | `11111111-1111-1111-1111-111111111111` | Log claims, change status, submit and retract reserves |
| Samuel Ortiz | supervisor | `22222222-2222-2222-2222-222222222222` | Also approve/reject reserves up to $100,000 and reopen claims |
| Maria Chen | manager | `33333333-3333-3333-3333-333333333333` | Also approve/reject any amount and set the $10M limit override |

**Screens.**
- **Claims** (`/claims`): filterable, paginated register; filters live in the URL.
- **Log New Claim** (`/claims/new`): three-step FNOL intake with policy typeahead, in-force check, parties and risk objects, optional initial reserve with the authority indicator.
- **Claim detail** (`/claims/{id}`): header with status transitions, then Overview, Parties, Reserves, Documents and Audit Log tabs.

**Structure** (`src/app`): `api/` typed HTTP services and models (the only place HTTP is used), `core/` mock auth, interceptors and notifications, `layout/` shell, sidebar and top bar, `shared/` reusable components and helpers, `features/` one lazy-loaded route per screen.

**Deployment.** `.github/workflows/frontend-ci.yml` builds and tests on pull requests; `frontend-cd.yml` deploys `main` to the Azure Static Web App via OIDC (see `iac/README.md` for the required GitHub variables). `public/staticwebapp.config.json` makes deep links such as `/claims/{id}` work on refresh.

## Status

Solution and workspace scaffolding only — no domain entities, migrations, or feature endpoints yet. See `docs/progress.md` for what's done and what's next.
