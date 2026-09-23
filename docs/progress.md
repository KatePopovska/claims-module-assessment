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

## Next Steps (Not Started)

- First vertical slice: `CreateClaimCommand` end-to-end (transactional child-record creation — `LossEvent`, `ClaimParty`, `ClaimRiskObject` — plus the `CLAIM_CREATED` audit entry via `IAuditLogService`)
- `POST /api/claims` controller endpoint + AutoMapper response DTO
- Remaining open questions in `docs/requirements.md` §9 (15 of 19 still open) don't block this — they can be resolved as the relevant feature is built, not all up front
