# Claims Module — FNOL & Reserve Management

**Live demo:** [**Open the app**](https://kind-cliff-0c3b95b0f.5.azurestaticapps.net/) · [**API (Swagger)**](https://app-claims-prod-api-cinzd2.azurewebsites.net/swagger/index.html)

No login needed: switch between the handler, supervisor and manager users in the top-right user menu (see [Application walkthrough](#application-walkthrough)).

A vertical slice of an insurance Claims Management module: **First Notice of Loss (FNOL) intake** and **reserve management** with authority-based approval, simulated GL posting and an immutable audit trail. .NET 9 Clean Architecture backend, Angular 21 frontend, deployed to Azure.

|                                                       |                                                               |
| ----------------------------------------------------- | ------------------------------------------------------------- |
| Frontend                                              | https://kind-cliff-0c3b95b0f.5.azurestaticapps.net/           |
| API (Swagger)                                         | https://app-claims-prod-api-cinzd2.azurewebsites.net/swagger/ |
| Architecture                                          | [ARCHITECTURE.md](ARCHITECTURE.md)                            |
| AI workflow                                           | [AI-WORKFLOW.md](AI-WORKFLOW.md)                              |
| Requirements, business rules, API contract, decisions | [`docs/`](docs)                                               |

## Repository layout

```
backend/                   .NET 9 solution (ClaimsModule.slnx)
  src/ClaimsModule.Domain           entities, domain rules, domain events, enums
  src/ClaimsModule.Application      MediatR commands/queries, validators, DTOs, interfaces, event handlers
  src/ClaimsModule.Infrastructure   Azure Blob / local storage, Hangfire jobs, current user, correlation id
  src/ClaimsModule.Persistence      EF Core DbContext, configurations, migrations, repository, Unit of Work, idempotency store
  src/ClaimsModule.API              controllers, middleware, mock authentication, Swagger, Hangfire host
  tests/                            Domain, Application and API unit tests (xUnit)
  database/seed/                    idempotent SQL seed scripts for reference data
frontend/                  Angular 21 application (Angular Material, standalone components, signals)
iac/                       Bicep templates for all Azure resources
.github/workflows/         CI and CD pipelines
docs/                      requirements extraction, domain model, business rules, API contract, decisions, progress log
docker-compose.yml         full local stack in containers
```

## Prerequisites

| Tool           | Version                                                            | Needed for                                  |
| -------------- | ------------------------------------------------------------------ | ------------------------------------------- |
| .NET SDK       | 9.0.316+ (`backend/global.json`)                                   | backend                                     |
| Node.js / npm  | 24.13.0 / 11.6.2 (`frontend/.nvmrc`; CI uses the same)             | frontend                                    |
| SQL Server     | LocalDB (installed with Visual Studio), SQL Server 2022, or Docker | database                                    |
| EF Core CLI    | `dotnet tool install --global dotnet-ef --version 9.0.20`          | applying migrations                         |
| Docker Desktop | any recent version                                                 | _alternative:_ run everything in containers |

## Run locally

### Option A — Docker (one command)

Starts SQL Server, applies the migrations, and runs the API and the Angular app. Docker is for local development only; Azure hosting does not use containers.

```
cp .env.example .env          # then set MSSQL_SA_PASSWORD in .env
docker compose up --build
```

| URL                            | What                      |
| ------------------------------ | ------------------------- |
| http://localhost:4200          | Angular app               |
| http://localhost:5160/swagger  | API (Swagger)             |
| http://localhost:5160/hangfire | Hangfire dashboard        |
| `localhost,1433` (user `sa`)   | SQL Server, e.g. for SSMS |

- `migrate` applies all migrations (including the reference data) and exits; the API starts only after it succeeds. The Docker database is separate from LocalDB and starts without claims.
- Uploaded documents use the local filesystem fallback (FRS BR-D-03) in the `uploads` volume.
- Ports 4200 and 5160 must be free. `SQL_PORT` in `.env` moves SQL Server if 1433 is taken. Avoid `;` in the SA password (it is embedded in a connection string).
- `docker compose down` stops everything; `docker compose down -v` also deletes the database and uploads.

### Option B — .NET and Node directly

1. **Database** (LocalDB by default):
   ```
   cd backend
   dotnet ef database update --project src/ClaimsModule.Persistence --startup-project src/ClaimsModule.API
   ```
2. **API** — http://localhost:5160, Swagger at `/swagger`, health at `/api/health`:
   ```
   dotnet run --project src/ClaimsModule.API
   ```
3. **Frontend** — http://localhost:4200, calls the API at `http://localhost:5160/api`:
   ```
   cd frontend
   npm ci
   npm start
   ```

### Tests

```
cd backend  && dotnet test --filter "Category!=Integration"      # unit tests
cd backend  && dotnet test tests/ClaimsModule.IntegrationTests   # integration tests — needs Docker running
cd frontend && npx ng test --watch=false
```

The integration tests start the real API in memory against a throwaway SQL Server 2022 container (Testcontainers), apply all migrations and run end-to-end scenarios: reference data, claim creation with audit and intake warnings, the claim-number sequence, the initial-reserve transaction, approval → Hangfire GL posting exactly once, `Idempotency-Key` replay and rejection, and tenant isolation. They run locally only; the pipelines run the unit tests.

## Configuration

Backend settings live in `backend/src/ClaimsModule.API/appsettings.json` (defaults) and `appsettings.Development.json` (local overrides); any key can also be set as an environment variable with `__` instead of `:`.

| Key                                     | Example                                                                                                   | Notes                                                                                                                                                                                                                 |
| --------------------------------------- | --------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings:DefaultConnection`   | `Server=(localdb)\mssqllocaldb;Database=ClaimsModule;Trusted_Connection=True;TrustServerCertificate=True` | Azure: `Server=tcp:<server>.database.windows.net,1433;Database=<db>;Authentication=Active Directory Managed Identity;Encrypt=True` — no password. Hangfire uses the same database and is enabled whenever this is set |
| `OrganisationId`                        | `00000000-0000-0000-0000-000000000001`                                                                    | the single tenant (FRS §15.1); every row is stamped with it and every query filtered by it                                                                                                                            |
| `Storage:Provider`                      | `LocalFileSystem` or `AzureBlob`                                                                          | outside Development the API refuses to start unless Azure Blob Storage is configured                                                                                                                                  |
| `Storage:LocalPath`                     | `uploads`                                                                                                 | local fallback folder, served under `/uploads` (Development only)                                                                                                                                                     |
| `Storage:AzureBlob:ServiceUri`          | `https://<account>.blob.core.windows.net`                                                                 | Azure: managed identity, links signed with a user-delegation key                                                                                                                                                      |
| `Storage:AzureBlob:ConnectionString`    | _(empty)_                                                                                                 | only for a local storage emulator                                                                                                                                                                                     |
| `Cors:AllowedOrigins:0`                 | `http://localhost:4200`                                                                                   | Azure: the Static Web App URL                                                                                                                                                                                         |
| `ASPNETCORE_ENVIRONMENT`                | `Development` / `Production`                                                                              | Development enables the local storage fallback and disables HTTPS redirection                                                                                                                                         |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | set by Bicep                                                                                              | Azure telemetry                                                                                                                                                                                                       |

Frontend API base URL: `frontend/src/environments/environment.development.ts` (`npm start`), `environment.ts` (production build, Azure API), `environment.docker.ts` (Docker image).

## Database, migrations and seed data

- Schema is created only by EF Core migrations (`backend/src/ClaimsModule.Persistence/Migrations`); nothing is created at application start-up.
- The initial migration inserts the reference data: 10 cause-of-loss codes and 5 simulated policies (FRS §5.5).
- `backend/database/seed/*.sql` contain the same reference data as idempotent `MERGE` scripts, for repairing or refreshing an existing database:
  ```
  sqlcmd -S <server> -d <database> -i backend/database/seed/01-cause-of-loss-codes.sql
  sqlcmd -S <server> -d <database> -i backend/database/seed/02-policies.sql
  ```
  Future reference-data changes go into these scripts **and** a migration that runs them, so a fresh database still needs nothing beyond `database update` (FRS §15.4).
- Claim numbers come from the SQL sequence `ClaimNumberSequence` (`CLM-{YYYY}-{0000000}`).

## Azure deployment

| Resource                                       | Purpose                                                                                                      |
| ---------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| App Service (Linux B1)                         | API + Hangfire jobs, system-assigned managed identity                                                        |
| Azure SQL Database                             | application data and Hangfire storage; Entra-only authentication, the API connects with its managed identity |
| Storage account                                | `claim-documents` container; shared-key access disabled, links signed with a user-delegation key             |
| Static Web App                                 | Angular frontend                                                                                             |
| Key Vault, Log Analytics, Application Insights | secrets store (currently none needed), logs and telemetry                                                    |

Everything is defined in Bicep under [`iac/`](iac/README.md), which also documents the one-time setup (Entra SQL admin, OIDC identity, database grant) and how to reproduce the deployment.

| Workflow            | Trigger                   | Does                                                                                               |
| ------------------- | ------------------------- | -------------------------------------------------------------------------------------------------- |
| `backend-ci`        | PR / push (`backend/**`)  | restore, build, unit tests                                                                         |
| `backend-cd`        | push to `main` / manual   | build, unit tests, publish, **apply EF migrations** (migration bundle), deploy the API, smoke test |
| `frontend-ci`       | PR / push (`frontend/**`) | `npm ci`, test, production build                                                                   |
| `frontend-cd`       | push to `main` / manual   | build, test, deploy to the Static Web App, smoke test                                              |
| `iac-ci` / `iac-cd` | PR / push (`iac/**`)      | Bicep build and lint / deploy the templates                                                        |

All deployments authenticate with **GitHub OIDC**; no client secrets, publish profiles, SQL passwords or storage keys are stored in GitHub. The required GitHub variables are listed in `iac/README.md`.

## Application walkthrough

There is no login screen (mock authentication, as allowed by the assessment). The user menu in the top-right corner switches between three users; every API request carries a Bearer token with the selected user's id and role.

| User         | Role       | Can                                                                              |
| ------------ | ---------- | -------------------------------------------------------------------------------- |
| Hannah Reed  | handler    | log claims, change status, submit, adjust and retract reserves, upload documents |
| Samuel Ortiz | supervisor | also approve/reject reserves up to $100,000 and reopen closed claims             |
| Maria Chen   | manager    | also approve/reject any amount and set the $10M reserve-limit override           |

A typical flow:

1. **Claims** (`/claims`) — filter by status, loss date, handler or cause of loss; filters live in the URL. **Log New Claim** starts the intake.
2. **FNOL intake** (`/claims/new`) — three steps: policy typeahead with an in-force badge (or _Unknown policy_), loss details; parties (at least one Claimant) and risk objects; an optional initial reserve with the live authority indicator (auto-approved ≤ $10,000, supervisor ≤ $100,000, manager above) and a review summary. Intake warnings (policy unknown, loss date outside the policy period, no risk objects) are confirmed in a dialog and recorded in the audit log.
3. **Claim detail** (`/claims/{id}`) — the header shows the claim and a **Change status** menu with only the valid next statuses (a pre-flight checklist before closing). Tabs:
   - **Overview** — loss details, severity, lifecycle timeline, recent activity, editable notes.
   - **Parties** — add parties; remove them (never the last active Claimant).
   - **Reserves** — add or adjust reserves in a side panel; above $10,000 the transaction waits for approval. Switch to the supervisor or manager to **Approve/Reject**; the submitter can **Retract**. Approved reserves are posted to the simulated GL by a background job; a failed posting can be retried.
   - **Documents** — upload (PDF, images, Office, text; 50 MB) with progress; links open a signed URL valid for one hour.
   - **Audit Log** — every event, newest first, read-only.

## Documentation

| File                                             | Content                                                                                         |
| ------------------------------------------------ | ----------------------------------------------------------------------------------------------- |
| [ARCHITECTURE.md](ARCHITECTURE.md)               | solution structure, data model, CQRS flow, domain events, jobs, Azure, decisions and trade-offs |
| [AI-WORKFLOW.md](AI-WORKFLOW.md)                 | how AI was used, example prompts, corrections                                                   |
| [docs/requirements.md](docs/requirements.md)     | requirements extracted from the FRS and the assessment brief, with open questions               |
| [docs/business-rules.md](docs/business-rules.md) | business rules and validation reference                                                         |
| [docs/domain-model.md](docs/domain-model.md)     | entities and columns                                                                            |
| [docs/api-contract.md](docs/api-contract.md)     | every endpoint with its behaviour                                                               |
| [docs/decisions.md](docs/decisions.md)           | decisions that resolve conflicts between the source documents                                   |
| [docs/progress.md](docs/progress.md)             | implementation log                                                                              |
| [iac/README.md](iac/README.md)                   | Azure infrastructure and deployment                                                             |
