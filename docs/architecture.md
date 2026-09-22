# Architecture Requirements

Source: Assessment §2.4, §3.8; FRS §15. This captures what the source documents *mandate* architecturally — it is a requirements extraction, not the actual design (that belongs in a future `ARCHITECTURE.md` deliverable per Assessment §4.4).

## 1. Required Backend Layering (Assessment §2.4)

Clean Architecture, five projects:

- **ClaimsModule.Domain** — entities, value objects, domain events, enumerations
- **ClaimsModule.Application** — MediatR commands/queries, validators, DTOs, interfaces, AutoMapper profiles
- **ClaimsModule.Infrastructure** — external services (Azure Blob, email, etc.), Hangfire job definitions
- **ClaimsModule.Persistence** — EF Core `DbContext`, migrations, repositories, Unit of Work
- **ClaimsModule.API** — ASP.NET Core controllers, middleware, configuration, startup

## 2. CQRS Requirements

- Every state-changing operation must be a MediatR **Command**; every read must be a MediatR **Query** (Assessment §2.4)
- Domain Events should be raised for significant state transitions (Assessment §2.4, §6.1 — e.g. `ClaimCreated` should trigger an audit log entry)
- Unit of Work pattern must coordinate persistence across repositories (Assessment §2.4, §6.1)
- FluentValidation must be wired at the **MediatR pipeline level** (pipeline behavior), not only at the controller (Assessment §6.1 — this is explicitly called out as something reviewers check)
- AutoMapper profiles must live in the Application layer, not the API layer (Assessment §6.1)

## 3. Data Conventions (FRS §15) — Mandatory, Not Optional

All tables/EF configurations must consistently follow these:

| Convention | Rule |
|---|---|
| Primary Keys | `UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID()` — sequential GUIDs to avoid index fragmentation |
| Monetary Amounts | `DECIMAL(19,4)` — never `money` or `float` |
| Timestamps | `DATETIMEOFFSET(7)`, stored UTC |
| Short Codes | `NVARCHAR(50)` |
| Names | `NVARCHAR(255)` |
| Free Text | `NVARCHAR(MAX)` |
| Boolean Flags | `BIT NOT NULL DEFAULT 0` |
| Soft Delete | `IsDeleted BIT NOT NULL DEFAULT 0` + `DeletedAt DATETIMEOFFSET(7) NULL`, on all master/transaction tables |
| Audit Columns | `CreatedAt` (not null), `UpdatedAt` (nullable), `UserCreated` (nullable), `UserModified` (nullable) — required on all tables |
| Tenant Isolation | `OrganisationId UNIQUEIDENTIFIER NOT NULL` on every business table; seed a single fixed `OrganisationId` |
| Optimistic Concurrency | `RowVer ROWVERSION NOT NULL` on aggregate roots (Claims, ClaimReserveComponents) |

### EF Core Configuration Requirements (FRS §15.2)

- Fluent API (`IEntityTypeConfiguration<T>`) — not data annotations
- Global query filter for soft delete: `HasQueryFilter(e => !e.IsDeleted)`
- All `decimal` properties: `HasPrecision(19, 4)`
- `RowVer` configured `IsRowVersion()`
- `NEWSEQUENTIALID()` as default value expression for GUID PKs

> **Note (missing tenant-isolation requirement, requirements.md §9.16):** FRS §15.2 explicitly mandates a global query filter for soft delete (`HasQueryFilter(e => !e.IsDeleted)`) but does not mandate an equivalent global filter for `OrganisationId`, even though §15.1 requires the column on every business table. Since the assessment seeds only a single fixed `OrganisationId`, this has no observable effect here, but a real multi-tenant deployment would need one. Recommended as a defense-in-depth addition even though it is not a stated hard requirement.

## 3a. Concurrency & Idempotency Requirements (consolidated)

Pulled together here because they're scattered across FRS sections and easy to under-implement individually:

| Mechanism | Requirement | Source |
|---|---|---|
| Claim number generation | Atomic DB-level counter increment (`UPDATE ... OUTPUT INSERTED` pattern or equivalent) to prevent duplicates under concurrent submissions; a `SEQUENCE` object or a dedicated sequence table with optimistic concurrency are both acceptable. **This technique is specified, not left to implementer discretion.** | FRS §5.3 |
| Aggregate-root writes | `RowVer ROWVERSION NOT NULL` optimistic concurrency on `Claims` **and** `ClaimReserveComponents` | FRS §15.1 |
| Reserve mutation | No `UPDATE` on a reserve record ever — every change is an `INSERT` into `ReserveHistory`; `CurrentAmount` is derived. This sidesteps lost-update races on reserve balances entirely rather than relying on locking. | FRS §6.6 |
| GL posting job | Idempotency key `Reserve:{ReserveComponentId}:Change:{ChangeSequence}`; must check the key before any write and no-op if already `Posted`; safe to run concurrently/retried | FRS §6.5, §12.1, BR-R-06 |
| SLA monitoring job | Only write a new `SLA_BREACH_DETECTED` entry if ≥24h have passed since the claim's last one — prevents duplicate entries across successive 15-minute runs | FRS §12.2 |
| API writes generally | Support an `Idempotency-Key` request header | FRS §10 intro |

### Naming Conventions (FRS §15.3)

| Item | Convention | Example |
|---|---|---|
| DB tables | PascalCase, plural noun | `Claims`, `LossEvents`, `ClaimParties` |
| DB columns | PascalCase | `ClaimId`, `LossDate`, `CauseOfLossCode` |
| C# entities | PascalCase, match table name | — |
| C# DTOs | Suffixed `Dto` or `Request`/`Response` | `CreateClaimCommand`, `ClaimDetailDto` |
| API routes | kebab-case plural nouns | `/api/claims`, `/api/cause-of-loss-codes` |
| MediatR commands | `VerbNounCommand` | `CreateClaimCommand`, `ApproveReserveCommand` |
| MediatR queries | `GetNounQuery` / `ListNounsQuery` | `GetClaimDetailQuery`, `ListClaimsQuery` |

> FRS §15.3 literally labels this convention "PascalCase singular noun" while its own examples (`Claims`, `LossEvents`, `ClaimParties`) are plural — a self-contradiction in the source. This spec follows the examples (plural), not the label text (requirements.md §9.17).

### Seeding & Migration Constraints (FRS §15.4)

- All seed data (cause-of-loss codes, policy records, reference statuses) applied via EF Core `HasData` or migration-time insert scripts — **not** application-startup code
- Migrations must be reproducible from scratch (`dotnet ef database update` on a fresh SQL Server instance)
- Application must start successfully with zero manual DB setup beyond running migrations
- No hard-coded GUIDs in application logic — use configuration or seeded reference IDs

## 4. Background Processing

Two Hangfire jobs required, both registered at application startup: `PostGLReserveChangeJob` (fire-and-forget, on reserve approval) and `SlaMonitoringJob` (recurring, every 15 minutes). See `docs/business-rules.md` §11 for full behavior/idempotency specs.

## 5. Storage Abstraction (FRS §7.4, §13)

- Documents stored in Azure Blob Storage (container `claim-documents`), path `{organisationId}/{claimId}/{sanitisedFilename}`
- Must sit behind an `IStorageService` interface with `AzureBlobStorageService` and `LocalFileSystemStorageService` implementations (also called out in Assessment §6.1 as something reviewers check — "wrapped behind an interface for testability")
- Provider selected via `appsettings.json`: `StorageProvider: AzureBlob | LocalFileSystem`
- Local fallback path: `/uploads/{organisationId}/{claimId}/`

## 6. Azure Deployment Requirements (Assessment §3.8, §4.3)

Minimum resources:

- Azure App Service (backend)
- Azure Static Web App or a second App Service (frontend)
- Azure SQL Database or SQL Managed Instance
- Azure Blob Storage account
- Azure Key Vault (optional, "demonstrates maturity")

Deliverable state: publicly accessible backend (Swagger populated) and frontend, both live at time of the live review session. Free/trial tier acceptable.

## 7. CI/CD Requirements (Assessment §2.3, §3.8)

- GitHub Actions or Azure DevOps Pipelines
- Pipeline must at minimum: build backend, run EF migrations, build frontend, deploy both to Azure
- Manually-triggered pipeline is acceptable — full automation on every push is not required

## 8. Frontend Architecture (FRS §11.4, Assessment §3.7.4)

- Angular routing with **lazy-loaded feature modules**: `ClaimsList`, `ClaimDetail`, `FnolIntake`
- All API communication through a **typed Angular service layer** — no direct HTTP calls in components (explicitly repeated in both source documents as a hard requirement)
- Mock authentication service: hardcoded users for handler/supervisor/manager; HTTP interceptor adds Bearer token to all requests; UI shows logged-in user + role; role switcher acceptable for testing only
- Angular Material theming with a custom primary palette
- Centralized HTTP error handling at the service layer → Material snackbar notifications
- Loading states on all async operations (spinner/skeleton); buttons disabled during pending calls
- Reactive Forms throughout
- Responsive down to 1280px minimum width (FRS) — not required to be mobile-responsive

## 9a. Detailed Screen Requirements (FRS §11.1–§11.3)

**Previously missing from this file (review pass, 2026-09-22):** §8 above only captured the FRS's *general* frontend requirements (§11.4). The FRS also specifies detailed, per-screen UI behavior that isn't optional polish — it's part of the spec. Recorded here since there is no separate frontend-behavior document among the six requirements files.

### Claims List (Dashboard) — FRS §11.1

- Paginated table columns: Claim Number, Policy Number, Client Name, Loss Date, Cause of Loss, Status (badge), Total Reserve Amount
- Status badge colors (fixed, not designer's choice): Draft = grey, Open = blue, UnderInvestigation = orange, PendingPayment = purple, Closed = green, Reopened = amber, Withdrawn = dark grey
- Filter bar: Status (multi-select), Date range (loss date from/to), assigned handler (text search), cause of loss (dropdown)
- Each row clickable → navigates to Claim Detail
- "Log New Claim" primary button → opens FNOL intake form
- Total claim count + page size selector
- Empty state for no matching claims

### FNOL Intake Form — FRS §11.2

Multi-step reactive form, each step validates independently before advancing:

- **Step 1 (Policy & Loss Details):** policy search typeahead (populates policy number/client name/effective dates on select); an in-force indicator badge (green if loss date within policy period, amber if expired/out-of-window); an "Unknown Policy" toggle to proceed without a linked policy; Loss Date picker (future-date validation error); Cause of Loss searchable dropdown (from `/api/reference/cause-of-loss-codes`); Loss Description textarea with a live character-count indicator (20-char minimum); Loss Location free text; optional Estimated Loss Amount.
- **Step 2 (Parties & Risk Objects):** inline add-party rows (role, type, name, email, phone) with an inline warning if no Claimant added yet; inline add-risk-object rows (asset type, description, damage description, optional reference) with an advisory message if none added; both sections render added items as removable chips/cards.
- **Step 3 (Initial Reserve & Review):** optional reserve entry (component dropdown + amount) with a **real-time authority-threshold indicator** showing one of: "✓ Auto-approved (≤ $10,000)" / "⚠ Supervisor approval required" / "⚠ Manager approval required"; a read-only review summary table; Create Claim button disabled until Steps 1–2 are valid, with a confirmation dialog if any Warning issues exist; on success, navigate to Claim Detail with a snackbar showing the generated Claim Number; on error, inline + top-of-step validation messages.

### Claim Detail Screen — FRS §11.3

- **Header (always visible):** Claim Number (copyable), status chip, Policy Number + Client Name, Loss Date + Cause of Loss, a transition control showing only the currently valid next statuses (with a pre-flight checklist specifically for the Closed transition), Assigned Handler.
- **Tab 1 — Overview:** loss event details, severity badge, estimated loss amount, editable claim notes.
- **Tab 2 — Parties:** role badge + contact details per party; Add Party button; Remove button per party, disabled for the last Claimant.
- **Tab 3 — Reserves:** one summary card per component (current balance + pending amount, amber if pending); transaction history table (date, type, component, amount colored green/red for increase/decrease, status badge, submitted by, approved by); Add Reserve slide-in panel with a real-time authority indicator; Approve/Reject buttons on `PendingApproval` rows, visible only to Supervisor/Manager; Retract button for the submitter's own pending rows; a GL Posting Status badge per row (Pending/Posted/Failed, with a retry action for Failed).
- **Tab 4 — Documents:** list (name, type, upload date, uploader, size); Download button (fetches SAS URL, opens new tab); Upload button (multipart, with progress indicator).
- **Tab 5 — Audit Log:** reverse-chronological, paginated, read-only (no edit/delete controls in the UI, matching BR-A-01).

## 9. Open Questions Affecting Architecture

- **Status-transition enforcement:** application-code state machine (per FRS §4.2/§7.3's explicit rule table) vs. a data-driven `ClaimStatusTransitions` table (per Assessment §3.2). Since the FRS provides the complete, authoritative rule set and Assessment §3.2 says schema "does not need to be copied verbatim," a data-driven table *can* be used as an implementation detail to store the same FRS-defined transitions — but the enforcement logic and validation messages must match FRS §4.2/§7.3 exactly regardless of storage approach. See requirements.md §9.2.
- **Manager override flag storage** — no column defined in FRS §9.1; needs to be added to the Claims schema at design time. See requirements.md §9.8.
