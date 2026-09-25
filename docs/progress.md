# Progress

## 2026-09-22 — Requirements Extraction

- Read both source documents in full: `docs/source/Claims_Module_Candidate_Specification.docx` (FRS, v1.0) and `docs/source/DICEUS_Fullstack_Technical_Assessment.docx` (Assessment Brief, v1.0). Both were originally at the repo root and moved into `docs/source/` for organization.
- Extracted and normalized requirements into:
  - `docs/requirements.md` — scope, tech stack, roles, deliverables, evaluation criteria, and a consolidated list of 10 conflicts/gaps found between the two source documents (see its §9)
  - `docs/domain-model.md` — entities, relationships, field definitions per FRS §9
  - `docs/business-rules.md` — status lifecycle, transition rules, closure conditions, FNOL/reserve/document/party/audit rules, background job specs, validation table
  - `docs/api-contract.md` — full endpoint list, error response shape, cross-cutting API requirements
  - `docs/architecture.md` — Clean Architecture layering, CQRS requirements, data conventions, Azure/CI-CD requirements, frontend architecture requirements
- No application code was written in this pass, per instruction.
- No missing requirements were invented. Every conflict between the FRS and the Assessment Brief, and every gap where a business rule implies a data element the schema doesn't define, is recorded as an open question/assumption rather than silently resolved. See `docs/requirements.md` §9 for the full list (10 items), most notably:
  - Conflicting closure path rigidity (FRS allows direct Open→Closed; Assessment implies a forced sequential path)
  - Conflicting reserve-adjustment endpoint shape (FRS: single POST; Assessment: separate PUT, which is hard to reconcile with the FRS's event-sourced reserve model)
  - Missing `OverrideFlag` column on Claims despite BR-R-05 requiring one
  - Undefined `Severity` field semantics on Claims

## 2026-09-22 — Documentation Review Pass

Reviewed all six generated documents against both source DOCX files for: missing requirements, invented requirements, contradictions between files, incorrect entity relationships, incorrect reserve rules, incorrect status transitions, missing authorization rules, missing tenant isolation requirements, missing concurrency requirements, missing audit requirements, and unsupported API endpoints. No application code touched.

**Findings and fixes (documentation only):**

- **Entity relationship bug (fixed):** Policy↔Claim cardinality in `docs/domain-model.md` was wrong (modeled as 1-to-0..1, wrongly capping a policy to one claim). Corrected to 1-to-many.
- **Missing schema field (fixed):** `ClaimReserveComponents.RowVer` (FRS §15.1 names this table explicitly as needing optimistic concurrency) was missing from the domain model's field list. Added.
- **Missing concurrency detail (fixed):** FRS §5.3's specific atomic claim-number-generation technique had been flattened to "generated atomically." Restored, and a new consolidated "Concurrency & Idempotency Requirements" section added to `docs/architecture.md`.
- **Missing validation rule (fixed):** FRS §5.4's "no risk objects linked" Warning example was absent from the validation table. Added to `docs/business-rules.md`.
- **Missing UI requirements (fixed):** FRS §11.1–§11.3's detailed screen specs (dashboard columns/badge colors, FNOL form fields per step, Claim Detail tab contents) were previously summarized away to general principles only. Full detail added to `docs/architecture.md`.
- **Internal inconsistency (fixed):** the GL job idempotency key's `{ReserveId}` placeholder was clarified in one place but not another; now consistent everywhere.
- **API endpoints:** verified all 17 endpoints in `docs/api-contract.md` trace directly to FRS §10; no invented endpoints found; the two Assessment-only endpoints not in the FRS remain correctly excluded and flagged.
- **Status transitions and reserve rules:** verified transcription against FRS §4.2 and §6–7 line by line — no incorrect transitions or reserve rules found; existing content was accurate.
- **New open questions surfaced (9 items, requirements.md §9 items 11–19):** handler-assignment-for-Open ambiguity, reserve submission-cap ambiguity (a genuine missing-authorization-rule gap), a dangling "BR-CL-01" reference in the FRS, a missing audit EventType for the Manager override flag, ambiguity on single-vs-dual audit entries for Closed/Reopened, tenant-isolation query filter not mandated (though harmless given single-org seeding), a self-contradictory naming-convention label in the FRS, correlation-ID handling for background-job audit entries, and whether the FRS's 422-for-authorization-failure pattern generalizes beyond reserve approval.

Total open questions/conflicts now stands at 19 (up from 10), all catalogued in `docs/requirements.md` §9, with straightforward doc-only corrections logged separately in §10 of that file.

## 2026-09-22 — Blocking Decisions Made; Proceeding to Scaffolding

Project owner resolved the 4 open questions that would have affected initial architecture/schema (documented in `docs/decisions.md` as ADR-001 through ADR-004):

- **ADR-001:** status transitions follow the FRS's explicit table as-is; the Assessment's stricter sequential closure path is not adopted (not a scaffolding blocker either way).
- **ADR-002:** reserve adjustment exposed as `PUT /api/claims/{id}/reserves/{reserveComponentId}` externally, but implemented internally as an `INSERT` into `ReserveHistory` (never an `UPDATE`), consistent with the FRS's event-sourcing rule. `POST` remains for opening a new component.
- **ADR-003:** Manager override flag stored on `Claims` (aggregate-level, since BR-R-05's $10M check is claim-wide): `OverrideFlag`, `OverrideByUserId`, `OverrideAt`, `OverrideReason`. New audit event type `RESERVE_OVERRIDE_SET` added to cover it.
- **ADR-004:** no additional per-transaction reserve submission cap invented; a Handler may submit any amount, the threshold table only governs the resulting approval status.

`docs/domain-model.md`, `docs/business-rules.md`, and `docs/api-contract.md` updated to reflect these decisions; corresponding open-question entries in `docs/requirements.md` §9 marked resolved with pointers to `docs/decisions.md`.

Proceeding to solution scaffolding next (Clean Architecture backend skeleton + Angular workspace skeleton — no feature code yet).

## 2026-09-22 — Solution Scaffolding

Backend: created `backend/ClaimsModule.sln` with five Clean Architecture projects (Domain, Application, Infrastructure, Persistence, API — net9.0, wired per `docs/architecture.md` §1). Added and verified: MediatR + FluentValidation pipeline behavior (`ValidationBehaviour`), AutoMapper, EF Core 9 + SQL Server (`ClaimsDbContext`, empty — no entities yet), `IStorageService` with both `AzureBlobStorageService` and `LocalFileSystemStorageService` implementations (FRS §7.4/§13), Hangfire wiring (dashboard + server, conditional on a configured connection string), a mock Bearer authentication handler (`MockAuthenticationHandler`, matches FRS §3's "mock auth is acceptable"), and exception-handling middleware that maps `FluentValidation.ValidationException` to the FRS §10.4 structured 422 error body. `Domain/Common` has base abstractions (`BaseEntity`, `BaseAuditableEntity`, `ISoftDelete`, `IDomainEvent`) matching the FRS §15.1 conventions — no concrete entities yet. Verified: solution builds clean, API runs, `/api/health` and Swagger UI both respond.

Frontend: created `frontend/` as an Angular 19 workspace (satisfies "Angular 18+"; picked over the environment's default CLI 22 due to a Node engine mismatch) with Angular Material, SCSS, standalone components, and lazy-loaded routes per feature area (`claims-list`, `fnol-intake`, `claim-detail` — confirmed as separate chunks in the build output, satisfying FRS §11.4's lazy-loading requirement). Added: mock `AuthService` (3 hardcoded users/roles + a role switcher in the toolbar, matching FRS §11.4), `authInterceptor` (adds the mock Bearer token to every request) and `errorInterceptor` (parses the FRS §10.4 error shape into Material snackbars) via `provideHttpClient(withInterceptors(...))`, and a `HealthService` as the first example of the required typed-service-layer pattern (no direct HTTP calls in components). Verified: `ng build` succeeds (three lazy chunks confirmed), `ng test` passes (9/9), `ng serve` serves both `/` and `/claims`.

Known rough edges from package-version churn during scaffolding (all resolved): pinned `Swashbuckle.AspNetCore` to 6.9.0 (latest 10.x pulled an incompatible pre-release `Microsoft.OpenApi` 2.x and crashed at runtime with `ReflectionTypeLoadException`) and removed the now-redundant `Microsoft.AspNetCore.OpenApi` template package; pinned EF Core packages to the `9.*` range (latest floated to a net10.0-only 10.x); used `FrameworkReference` instead of the legacy standalone `Microsoft.AspNetCore.Http.Abstractions` NuGet package for `IHttpContextAccessor` in the Infrastructure class library.

No feature/business logic implemented yet (no Claim/Reserve/Policy entities, no MediatR commands or queries, no controllers beyond the health check). That's the next phase.

## 2026-09-23 — Domain Entities, EF Configurations, Repositories + Unit of Work

Implemented all 10 entities from `docs/domain-model.md` (`ClaimsModule.Domain`) with `IEntityTypeConfiguration<T>` classes in `ClaimsModule.Persistence` applying the full FRS §15 convention set (NEWSEQUENTIALID() GUID PKs, DECIMAL(19,4), DATETIMEOFFSET(7), soft-delete global query filter, RowVer on Claims/ClaimReserveComponents only). Generated and verified an `InitialCreate` EF Core migration. Added Central Package Management, `Directory.Build.props`, and `global.json` to the backend solution beforehand to prevent version drift across the five projects.

Added an explicit repository + Unit of Work layer (`IClaimRepository`, `IPolicyRepository`, `ICauseOfLossCodeRepository`, `IUnitOfWork` in Application; implementations in Persistence) after re-checking the Assessment brief's project-structure line for `ClaimsModule.Persistence` ("EF Core DbContext, migrations, **repositories, Unit of Work**") — the initial `IApplicationDbContext`-with-`DbSet<T>` design alone didn't literally satisfy that. Repositories are scoped to what **commands** need (load-one-aggregate-to-mutate-it); reads/dashboards/reporting stay on `IApplicationDbContext` directly — repositories are not used for arbitrary querying.

Two domain-model corrections caught during a design review of the entities against the FRS source:
- `LossEvents.CauseOfLossCode` was modeled as a `Guid` FK to `CauseOfLossCodes.CauseOfLossCodeId`. FRS §9.2 explicitly specifies a string FK to `CauseOfLossCodes.Code` (NVARCHAR(50)) instead. Fixed in the entity, EF config (`CauseOfLossCodes.Code` is now an EF Core alternate key), and `docs/domain-model.md`.
- `Claims.OverrideFlag`/`OverrideByUserId`/`OverrideAt`/`OverrideReason` (added in ADR-003 to cover a genuine FRS schema gap) renamed to `ReserveLimitOverride*` — the generic `Override*` naming didn't make clear which of possibly several future override concepts it covers. Updated across `docs/decisions.md`, `docs/domain-model.md`, `docs/business-rules.md`, `docs/requirements.md`.

`InitialCreate` was regenerated from scratch after both fixes (confirmed via `sqlcmd` that it had never been applied to any local database, so no second migration was needed on top).

Also removed the speculative `IClaimRepository`/`IPolicyRepository`/`ICauseOfLossCodeRepository` added earlier the same day — none had a real caller yet (no commands exist), every method was a guess at a future shape. Kept `IUnitOfWork` since it's already at its minimal, non-speculative shape (one method, directly required by the brief's "Unit of Work" line). Repositories will be rebuilt with exactly the methods each command needs, in the same change as that command.

## 2026-09-23 (cont'd) — Seed Data, Claim Number Sequence, Audit Log Service

Closed out the three foundational pieces every later command depends on:
- **Seed data**: 5 policies (FRS §5.5) and 10 cause-of-loss codes (FRS §5.6) via EF Core `HasData` on `PolicyConfiguration`/`CauseOfLossCodeConfiguration` (FRS §15.4 requires migration-time seeding, not startup code). Also caught and fixed the same org-scoping mistake on `Policies.PolicyNumber`'s unique index that was just fixed for `CauseOfLossCodes.Code` — FRS §9.10 states it as plainly "unique," not "unique per organisation" (unlike `ClaimNumber`, which BR-C-04 explicitly scopes per org).
- **Claim number generator**: a SQL Server `SEQUENCE` (`ClaimNumberSequence`, registered via `modelBuilder.HasSequence<int>`) behind `IClaimNumberGenerator`/`ClaimNumberGenerator`, per FRS §5.3's specified technique. Verified against the real database via `sqlcmd`.
- **`IAuditLogService`**: the one permitted writer to `ClaimAuditLog` (BR-A-01 — no handler writes to that table directly). Stages the entry via `IApplicationDbContext` but does not call `SaveChangesAsync` itself, so it always commits atomically with the business change it describes, via the caller's later `IUnitOfWork.SaveChangesAsync()`. Needed a new `ICorrelationIdProvider` (Application interface, `HttpContext.TraceIdentifier`-backed implementation in Infrastructure) to satisfy FRS §14.2's correlation-ID propagation.

`InitialCreate` regenerated again to include the sequence and seed `INSERT`s, and applied to LocalDB (`dotnet ef database update`) — verified via `sqlcmd` that all 10 cause-of-loss codes, all 5 policies (with correctly computed `Active`/`Expired` status based on today's date, not hardcoded), and the sequence all work end-to-end.

## 2026-09-23 (cont'd) — First Vertical Slice: `CreateClaimCommand`

`POST /api/claims` end-to-end: `CreateClaimCommand`/`CreateClaimCommandValidator`/`CreateClaimCommandHandler` in `ClaimsModule.Application`, `ClaimsController` in the API, AutoMapper profile (`ClaimMappingProfile`) for the response DTO. Reintroduced `IClaimRepository` with exactly one method (`Add`) — the one this command actually calls — closing out the repository discussion from earlier the same day.

**Scoping decision:** FluentValidation on this command enforces only the FRS's Critical-severity rules (BR-C-01 loss date not future, BR-C-07 description ≥20 chars, BR-C-05 cause-of-loss code exists+active, plus a structural check that a supplied `PolicyId` references a real policy). Warning-severity rules (BR-C-02 policy period, BR-C-06 no policy linked, no Claimant yet, no risk objects) do **not** block creation — matching `business-rules.md` §1's own description of Draft as "created but incomplete/unvalidated." No `ValidationIssue` entity exists anywhere in `domain-model.md`'s entity list despite `VALIDATION_ISSUE_ADDED` being a defined audit event — flagging this as a genuine documentation gap (not one of the original 19) rather than inventing an entity to fill it. Current read: BR-ST-02's "no unresolved Critical validation issues" is meant to be evaluated live against the claim's current data at the moment of the `Draft → Open` transition attempt, not against a persisted issue log — to be confirmed when `UpdateClaimStatusCommand` is built.

**Bugs found and fixed via real end-to-end testing (not just compilation) against the actual LocalDB:**
- `IAuditLogService.Log` took a `Guid claimId` — but `Claim.Id` is `Guid.Empty` until after `SaveChangesAsync` runs (DB-generated `NEWSEQUENTIALID()`), so the very first audit entry (`CLAIM_CREATED`) would have been written with a garbage FK. Changed the signature to take the `Claim` entity itself, so EF Core's own FK-fixup resolves the real generated Id within the same `SaveChangesAsync` call — no second round-trip needed, still atomic.
- System.Text.Json didn't accept enum values as strings (`"Claimant"` failed to parse) — added `JsonStringEnumConverter` globally.
- `ClaimNumberGenerator` used `Database.SqlQueryRaw<int>(...).SingleAsync(...)` — EF Core composes that as a derived table once any LINQ operator is chained on, and SQL Server rejects `NEXT VALUE FOR` inside a subquery (`Error Number:11719`). Rewrote it to run as a standalone statement over the raw ADO.NET connection EF Core already manages (`context.Database.GetDbConnection()` + `ExecuteScalarAsync`), bypassing EF's query composition for just this one call.
- API routes were rendering as `/api/Claims` (PascalCase from the `[controller]` token) instead of FRS §15.3's mandated kebab-case — added `options.LowercaseUrls = true` globally.

**Verified against the real database** (not just Swagger 200s): claim number `CLM-2026-0000001` format correct; `Claims`/`LossEvents`/`ClaimParties`/`ClaimRiskObjects`/`ClaimAuditLog` rows all correctly linked by FK; `ClaimAuditLog.CorrelationId` populated from `HttpContext.TraceIdentifier`; 422 structured error body for multi-field validation failures matches FRS §10.4 exactly; 401 for unauthenticated requests; 201 for a claim with no policy linked (correctly not blocked, per the scoping decision above).

## 2026-09-23 (cont'd) — First Two Read-Side Queries

Before starting frontend work, prioritized the two reference-data queries that FNOL Step 1 (policy typeahead, cause-of-loss dropdown) needs, over continuing further write-side commands:

- `GetCauseOfLossCodesQuery` — `GET /api/reference/cause-of-loss-codes`, optional `?perilCategory=` filter (bound directly as a nullable enum query param — ASP.NET Core's model binder already accepts enum names from query strings natively, no extra config needed, unlike the JSON body case which needed `JsonStringEnumConverter`).
- `SearchPoliciesQuery` — `GET /api/policies/search?q=...`, partial match on policy number or client name; empty/missing `q` returns all seeded policies rather than none, since an empty state is more useful for a typeahead than a dead end.

Both go straight through `IApplicationDbContext` with `.AsNoTracking()` — no repository involved, per the established read/write split. Verified against the real seeded data (all 10 cause-of-loss codes, peril-category filtering, policy search by both number and client name) — all five test cases passed with no bugs found, unlike `CreateClaimCommand`'s slice.

## 2026-09-24 — Status Transitions

`PUT /api/claims/{id}/status` — `ClaimStatusTransitions` (Domain: a static rule table for BR-ST-01/FRS §4.2, application-code enforcement per ADR-001) + `UpdateClaimStatusCommand`/Handler. Reintroduced `IClaimRepository.GetByIdAsync` and `NotFoundException` — both concretely needed by this command (load-and-mutate an existing claim, 404 if missing), not speculative.

**Resolves the "live-evaluated Critical issues" open question** (flagged 2026-09-23): no `ValidationIssue` entity exists in the domain model, so BR-ST-02(a)/CC-02 ("no unresolved Critical validation issues") is evaluated live against the claim's current `LossEvent` state (loss date not future, description ≥20 chars, cause-of-loss code still active) at the moment of the transition attempt, not against a persisted issue log. Decision logged here since nothing forced it either way.

**Decision on the single-vs-dual audit question** (requirements.md §9.15, still formally open): both fire — a generic `STATUS_CHANGED` entry plus the specific `CLAIM_CLOSED`/`CLAIM_REOPENED` event, since the FRS defines distinct payload semantics for each (`STATUS_CHANGED`'s old/new status pair vs. `CLAIM_CLOSED`/`CLAIM_REOPENED`'s reason) and nothing suggests they're meant to be exclusive alternatives.

`Reopened → Open` is implemented as "immediate, automatic" (business-rules.md §2) within the same command — the persisted `Status` lands directly on `Open`, never literally `Reopened`, but the audit trail records all three logical steps in order (`Closed→Reopened` STATUS_CHANGED, `CLAIM_REOPENED`, `Reopened→Open` STATUS_CHANGED).

**Bug found via real audit-trail inspection (not just a 200 response):** the primary `STATUS_CHANGED` entry was logging `claim.Status` *after* `ApplyTransition` had already auto-advanced it to `Open`, silently skipping over the requested `Reopened` value in its own audit record (`Closed→Open` instead of `Closed→Reopened`). Fixed to log the requested target status, not the post-auto-hop persisted one.

Verified end-to-end against the real database across 9 scenarios: invalid transition (422, lists valid next statuses), Draft→Open blocked by missing Claimant, valid Draft→Open, valid Open→Closed, Reopened blocked for Handler role, Reopened blocked for missing reason, valid Reopened as Supervisor (with the corrected audit trail), Withdrawn blocked for missing reason, PendingPayment blocked with no approved reserves, and 404 for a nonexistent claim.

An `OutOfMemoryException` mid-session turned out to be ~35 orphaned `MSBuild.exe`/`VBCSCompiler.exe` processes accumulated from the many background `dotnet run`/stop cycles across prior sessions — killed and rebuilt clean. Not a code issue.

### FRS review pass on the status transition slice

Business transition logic moved out of the handler into the Domain:

- `ClaimStatusTransitions` — the transition table now carries the permitted roles per transition. All transitions are open to handler/supervisor/manager (FRS §4.1/§4.2; supervisor and manager inherit handler capabilities per §3) except `Closed → Reopened` (supervisor/manager, BR-ST-04). A missing or unknown role is rejected on every transition (previously only Reopen was role-checked).
- `ClaimStatusChangePolicy.Evaluate` — pure evaluation returning blocking issues + acknowledged warnings. The handler maps issues to `ValidationException` (422).
- `Claim.ChangeStatus` — applies the transition and returns the steps taken, including the automatic `Reopened → Open` hop. The handler writes one audit sequence per step, so the earlier "log the requested target, not the persisted status" workaround is no longer needed.

Rule changes:

- **Warning acknowledgement (BR-C-02):** `Draft → Open` is blocked while the loss date is outside the linked policy's effective period, unless the request sets `acknowledgeWarnings: true`. Only BR-C-02 requires acknowledgement; "no policy linked" and "no risk objects" stay non-blocking (FRS §5.4). Acknowledged warnings are recorded in the `STATUS_CHANGED` description.
- **PendingPayment:** requires an `Active` reserve component whose current approved balance (sum of `Approved`/`AutoApproved` history amounts) is > 0. Previously any historical approved row counted, including one later fully reversed. CC-04 on close uses the same computed balance instead of the stored `CurrentAmount`, which nothing maintains yet.
- **Reopened → Open:** skips the Open entry checks (BR-ST-02), since the FRS makes it immediate and unconditional; `ClosedAt`/`ClosureReason` are cleared.
- **Audit:** the reason is now included in the `STATUS_CHANGED` description, so withdrawal reasons are recorded (previously lost — FRS §4.1 "withdrawal reason recorded"). The automatic hop is described as automatic and carries no reason.
- **Reason length:** capped at 500 chars by the validator, matching the `ClosureReason` column (a longer reason previously caused a 500 on save).

Verified end-to-end against the real database across 12 scenarios: Draft→Open on the expired policy blocked without acknowledgement, blocked for an unknown role, allowed with acknowledgement (warning recorded in audit); PendingPayment blocked with no reserves; 501-char reason rejected; Withdrawn with the reason in audit; Draft→Open without a policy not blocked; Open→Closed; Reopened blocked for handler, allowed for supervisor (persisted `Open`, closure fields cleared, full audit sequence); invalid Open→Draft lists valid next statuses; 404 for a missing claim.

**Known limitation found during verification:** audit entries written in one `SaveChangesAsync` share the same `CreatedAt`, so ordering by `CreatedAt` alone doesn't reliably reproduce their write order (e.g. the three Reopen entries). To be addressed when `GET /api/claims/{id}/audit` is built. *(Resolved in the next entry.)*

## 2026-09-24 (cont'd) — Claim Read Side for the Frontend

Four queries, all through `IApplicationDbContext` with `.AsNoTracking()` projections (read/write split unchanged — no repository methods added):

- `ListClaimsQuery` — `GET /api/claims`. Filters: `status` (repeatable, for the dashboard's multi-select), `dateFrom`/`dateTo` (loss date, inclusive), `assignedHandlerId`, `causeOfLossCode`, `policyId`, `search` (claim number or client name). Paged via `page`/`pageSize` (default 20, max 100), newest reported first. Returns `PagedResult<T>` (`items`, `totalCount`, `page`, `pageSize`) — the total count feeds FRS §11.1's "total claim count". Rows include cause-of-loss name (dashboard column) and `totalReserves`.
- `GetClaimDetailQuery` — `GET /api/claims/{id}`. Header fields plus `validNextStatuses` (for the transition button), loss event, parties (incl. inactive, for the active/inactive display), risk objects, reserve summary per component (`currentBalance` + `pendingAmount`, FRS §11.3 Tab 3 cards), documents (metadata only — SAS URLs belong to the documents endpoint), and the 10 most recent audit entries.
- `GetClaimAuditLogQuery` — `GET /api/claims/{id}/audit`, paged, reverse-chronological; 404 for a missing claim.
- `GetClaimStatusesQuery` — `GET /api/reference/claim-statuses`, straight from `ClaimStatusTransitions` (no DB).

**Reserve totals** use the same definition as the status-transition policy: sum of `Approved`/`AutoApproved` `ReserveHistory` amounts, computed in SQL — not the stored `CurrentAmount`, which nothing maintains until the reserve slice exists.

**Audit ordering fix:** `AuditLogService` now stamps `CreatedAt` itself, strictly increasing within a request (bumped by one tick if the clock hasn't moved), and `ClaimsDbContext.SaveChangesAsync` only fills `CreatedAt` when it's still unset. Entries from one save now sort in write order; verified on the Reopen sequence (`Closed→Reopened`, `CLAIM_REOPENED`, automatic `Reopened→Open`). No migration needed.

`AsSplitQuery` wasn't used on the detail projection — it lives in the relational EF package, which Application doesn't reference; one claim's child collections are small enough for a single query.

Verified end-to-end against the real database: every list filter's `totalCount` matched a direct SQL count (status multi-select, loss-date range, cause of loss, policy, handler, client-name search); paging (page 2 of size 2 on the audit log); 422 for bad paging and `dateTo < dateFrom`; 404 for detail and audit on a missing claim. Reserve figures checked against hand-inserted `ReserveHistory` rows (auto-approved add, approved negative adjustment, pending adjustment, rejected add): component balances, pending amounts, list and detail totals all correct. The same data also confirmed the status rules on a real reserve: `Open → PendingPayment` now allowed, and closing with a pending reserve returns both CC-01 and CC-04 failures.

Known gap: an unparseable enum in the query string (`?status=Nope`) gets ASP.NET's default `400` problem-details body, not the FRS §10.4 `422` shape — same as malformed JSON bodies on the existing `POST` endpoint. Not changed here.

Two readability refactors after the first pass, both verified to produce byte-identical SQL and responses (EF Core command logging captured before and after): `GetClaimDetailQueryHandler`'s nested projection split into named expressions in `ClaimDetailProjections`, and `ListClaimsQueryHandler`'s filter `if` chain replaced by a reusable `WhereIf` extension (`Common/Extensions/QueryableExtensions`).

## 2026-09-24 (cont'd) — Parties

`POST /api/claims/{id}/parties` (`AddClaimPartyCommand`) and `DELETE /api/claims/{id}/parties/{partyId}` (`RemoveClaimPartyCommand`). New `IClaimRepository.GetWithPartiesAsync` — both commands only need the claim and its parties, not the reserve history `GetByIdAsync` loads.

- **Last-Claimant rule** (FRS §7.5 / API §10.1): `Claim.IsLastActiveClaimant(party)` in the Domain; removing it returns 422.
- **Validation:** same person/company name rules as FNOL intake, plus max lengths matching the `ClaimParties` columns (a longer value would otherwise fail on save with a 500).
- **Idempotent DELETE:** removing an already-inactive party returns 204 without writing a second `PARTY_REMOVED` entry.
- **Audit:** `PARTY_REMOVED` records the party Id (`RelatedEntityId`/`RelatedEntityType = ClaimParty`) and `OldValue = "{Role}: {Name}"`. `PARTY_ADDED` records `NewValue = "{Role}: {Name}"` but **no** `RelatedEntityId`: party Ids are generated by the database (`NEWSEQUENTIALID()`), so the Id doesn't exist until the save that also writes the audit entry. The FRS doesn't require a related Id for `PARTY_ADDED`, but it does for `DOCUMENT_UPLOADED` (§14.1), so this needs a general fix before the documents slice (options: client-side sequential GUIDs for these entities, or resolving related Ids at save time).
- No role gate — all three roles can manage parties (FRS §3 lists it as a Handler capability).

Verified end-to-end against the real database across 14 scenarios: removing the only Claimant (422); adding a company Witness (201, full party returned); person without names and company without company name (422 per field); 51-char phone (422); adding a second Claimant then removing the first (204), repeating that removal (204, no extra audit entry), then removing the remaining Claimant (422); unknown party, unknown claim on remove and on add (404). The detail endpoint shows the removed party as `isActive: false`, and the audit log holds exactly the expected `PARTY_ADDED` ×2 / `PARTY_REMOVED` ×1 entries. An invalid enum in the body (`partyRole: "Nope"`) gets the framework's 400, the same known gap as above.

## 2026-09-24 (cont'd) — Reserves + GL Posting Job

Endpoints (see `docs/api-contract.md` §2): `GET`/`POST /api/claims/{id}/reserves`, `PUT .../reserves/{reserveComponentId}`, `POST .../reserves/{txnId}/approve|reject|retract`, and `PUT /api/claims/{id}/reserve-limit-override`.

**Structure**
- Domain: `ReserveAuthority` (thresholds and role checks), `ReserveRules` (every submission/approval/rejection/retraction/override rule, returning `BusinessRuleViolation`s), and behaviour on the entities — `ClaimReserveComponent.RecordTransaction` (sequence, balances, transaction type, idempotency key), `ReserveHistory.Approve/Reject/Retract/MarkPosted/MarkPostingFailed`, `Claim.GetApprovedReserveTotal/WouldExceedReserveLimit/SetReserveLimitOverride/FindReserveTransaction`. `ClaimStatusChangePolicy` now uses `ClaimReserveComponent.GetApprovedBalance()` instead of its own copy.
- Application: one command per action under `Reserves/Commands`, a shared `ReserveTransactionSubmitter` for `POST` and `PUT`, `GetClaimReservesQuery`, and `IGlPostingScheduler`. Transaction DTOs come from one projection (`ReserveProjections`), used both in SQL and in memory.
- Infrastructure: `PostGLReserveChangeJob` (Hangfire) and `HangfireGlPostingScheduler`. The job only sends MediatR commands; the posting logic lives in Application.

**Decisions where the FRS is silent**
- Authority thresholds apply to the **absolute** transaction amount (FRS §6.3 says "amount of the individual transaction"; a $150k reduction needs a Manager just like a $150k increase).
- A component with a `PendingApproval` transaction accepts no further changes until it's resolved — the reading of FRS §6.4's "a reserve in PendingApproval may not be modified … retract, then submit a new one". This also keeps `PreviousBalance`/`NewBalance` unambiguous.
- `PUT` accepts negative deltas (`Reverse`), but a non-subrogation component's approved balance can't go below zero (FRS §6.2 "May Go Negative? No"). `POST` follows BR-R-01 literally (`> 0`, non-zero for subrogation).
- BR-R-05: a change that would take the approved total over $10M is always `PendingApproval` (never auto-approved), returns the FRS warning text, and can't be approved until a Manager sets the override. The total includes `SubrogationRecoverable` (negative) balances, since BR-R-05 says "across all components".
- Rejection uses the same amount authority as approval (FRS §3: Supervisor may "approve/reject reserves up to $100,000"). Self-rejection is allowed — BR-R-03 only forbids self-approval.
- Override endpoint: `PUT /api/claims/{id}/reserve-limit-override` (ADR-003 defined the columns and audit event but no endpoint). Setting it twice is a 422.

**Ids before save:** the idempotency key `Reserve:{ReserveComponentId}:Change:{n}` needs the component Id when the history row is created, and the reserve audit entries need the transaction Id. The domain assigns these Ids itself: `Claim.OpenReserveComponent` and `ClaimReserveComponent.RecordTransaction` set `Id = SequentialGuid.NewGuid()` (Domain/Common — the same SQL Server-ordered algorithm as EF Core's `SequentialGuidValueGenerator`, so no index fragmentation). Reserves stay inside the Claim aggregate: handlers add them through the domain entities and persist everything with `IUnitOfWork.SaveChangesAsync()`; `IClaimRepository` has no reserve-specific methods and there is no `IReserveRepository`. Both Ids are configured `ValueGeneratedNever()` — without it, EF treats a new entity that already has a key as an existing row and issues an `UPDATE` (observed: `DbUpdateConcurrencyException`, 0 rows affected). The `NEWSEQUENTIALID()` column default is untouched and `dotnet ef migrations has-pending-model-changes` reports no changes — no migration. Other entities are unchanged; the same approach closes the `PARTY_ADDED`/`DOCUMENT_UPLOADED` related-Id gap noted in the Parties entry when documents are built.

**GL posting job (FRS §6.5, §12.1)**
- Enqueued after the save, on auto-approval and on manual approval, with `ReserveHistoryId`, `ClaimId`, `IdempotencyKey`.
- Re-entrant: a transaction already `Posted` is a no-op, and the audit entry and the `Posted` status are written in the same save, so a retry after a failure can't duplicate the entry. Mismatched keys and non-approved transactions throw.
- `[AutomaticRetry(Attempts = 10)]` (Hangfire's default). On the final attempt the job marks the row `Failed` and writes `GL_POSTING_FAILED` in a fresh DI scope, then rethrows so Hangfire records the job as failed.
- `PostingJobId` stores the Hangfire job id.

**Other changes**
- **Bug fix — validators never ran for commands without a response.** `ValidationBehaviour` was constrained to `IRequest<TResponse>`; with MediatR 14, `IRequest` (void) commands don't satisfy it, so their validators were silently skipped. This affected `RemoveClaimPartyCommand` since the Parties slice. Constraint changed to `where TRequest : notnull`; verified with an empty party id (now 422).
- `ExceptionHandlingMiddleware` maps `DbUpdateConcurrencyException` and unique-index violations to `409 Conflict` (component `RowVer` on concurrent approvals; the `(ReserveComponentId, ChangeSequence)` unique index on concurrent submissions).

**Verification** — a 45-check end-to-end script against the real database, all passing: policy required; auto-approval + GL posting; duplicate component, zero/negative amounts, negative subrogation; supervisor band, handler can't approve, self-approval (FRS message), a second supervisor can; manager-only band; approving a non-pending transaction; reject (reason required, role, `Cancelled` posting); resubmit after rejection (sequence 2, previous balance 0); retract (submitter only, not twice); balance floor and `Reverse`; balances/total/transaction list and the claim-detail total; the $10M limit (warning, no auto-approval, approval blocked, Manager-only override, reason required, not twice, approval after override); 404s. Direct DB checks confirmed `CurrentAmount`, balances, idempotency keys, job ids, and that only the 6 approved transactions were posted.

GL job resilience checked through Hangfire itself: requeueing a succeeded job ran it again with no second `GL_POSTING_SIMULATED` entry. The retries-exhausted path was simulated on test data (row reset to `Pending`, job argument given a wrong key, `RetryCount` set to 10): the row became `Failed`, `GL_POSTING_FAILED` recorded the reason, and Hangfire marked the job `Failed`.

**Not done / known gaps**
- The Hangfire enqueue happens after the database save, so a crash between the two leaves an approved transaction with `PostingStatus = Pending` and no job. There's no sweeper for that yet.
- No rule about reserves on `Closed`/`Withdrawn` claims — the FRS doesn't define one.

## Next Steps (Not Started)

- SLA monitoring job (FRS §12.2)
- Documents: upload/list with SAS URLs (use the client-side Id approach above for `DOCUMENT_UPLOADED`'s related Id)
- Remaining open questions in `docs/requirements.md` §9 (14 of 19 still open — the live-critical-issues question above is now resolved) don't block this — they can be resolved as the relevant feature is built, not all up front
