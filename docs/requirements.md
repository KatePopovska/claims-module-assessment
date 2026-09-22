# Requirements

Source of truth: `docs/source/Claims_Module_Candidate_Specification.docx` (**FRS**, v1.0, May 2026) and `docs/source/DICEUS_Fullstack_Technical_Assessment.docx` (**Assessment Brief**, v1.0, May 2026).

Where the two documents conflict, this file notes both positions under **Open Questions** rather than silently picking one. Everything else here is asserted directly by one or both source documents — nothing is invented.

## 1. What is being built

A standalone, greenfield **Claims Management System** vertical slice covering two workflows for the insurance domain:

1. **FNOL Intake** — recording a new claim from a reported loss event: policy lookup (simulated), loss details, parties, risk objects, validation, and claim creation.
2. **Reserve Management** — opening and adjusting financial reserves against a claim, with authority-tiered approval and a simulated GL posting job.

Supporting capabilities required to make the two workflows functional: claim status lifecycle, document upload/retrieval, and an immutable audit log. (FRS §1.3)

This is a technical assessment; the FRS is explicitly self-contained ("no additional business documentation is required" — FRS §Document Info) and the Assessment Brief frames it as a bounded, gradeable vertical slice, not the full Claims module (Assessment §2.2, §3.1).

## 2. In scope (FRS §2)

- FNOL / Claim Creation — multi-step intake, policy lookup, party entry, validation
- Claim Status Transitions — defined state machine with enforced transition validation
- Claim Parties — claimant required, additional parties optional
- Claim Risk Objects — simplified (asset type + description, no deep asset hierarchy)
- Reserve Management — open, adjust, approve/reject; GL posting simulation
- Reserve Authority Workflow — three-tier threshold (auto / supervisor / manager)
- Document Upload — Azure Blob Storage; SAS URL retrieval
- Audit Log — append-only event log per claim
- Policy Lookup (simulated) — seeded dataset; search by policy number or name
- SLA Monitoring Job — recurring Hangfire job flags stale open claims
- Claims List Dashboard — paginated, filterable Angular table
- FNOL Intake Form — multi-step Angular reactive form
- Claim Detail Screen — tabs: Overview / Parties / Reserves / Documents / Audit Log

## 3. Out of scope (FRS §2)

- Coverage Evaluation (coverage types are seeded reference data only, not evaluated)
- Claim Payments (no disbursement)
- Subrogation / Recoveries (as a workflow — note: `SubrogationRecoverable` still exists as a reserve *component type*, §6.2)
- Litigation Management
- Fraud / SIU Scoring
- Bulk Operations
- Communications / Messaging
- Reinsurance

## 4. Required technology stack (Assessment §2.3)

| Layer | Requirement |
|---|---|
| Runtime | .NET 9 / C# 13 |
| Web API | ASP.NET Core Web API |
| CQRS / Mediator | MediatR 12+ |
| ORM | Entity Framework Core 9 |
| Validation | FluentValidation |
| Mapping | AutoMapper |
| Background Jobs | Hangfire |
| Database | SQL Server 2022 (or Azure SQL) |
| Serialization | System.Text.Json / Newtonsoft.Json |
| File Storage | Azure Blob Storage (with local filesystem fallback, FRS §7.4/§13) |
| Frontend Framework | Angular 18+ |
| UI Component Library | Angular Material (or equivalent) |
| Forms | Reactive Forms |
| State Management | Candidate's choice (NgRx, signals, services) |
| Architecture Pattern | Clean Architecture |
| Deployment | Microsoft Azure (App Service or Container Apps) |
| Source Control | Git |
| CI/CD | GitHub Actions or Azure DevOps (basic pipeline) |

## 5. User roles (FRS §3)

Three roles, simulated via mock authentication:

- **Claims Handler** (`handler`) — create claims, manage parties, upload documents, open reserves within authority (≤ $10,000), view all claim data
- **Claims Supervisor** (`supervisor`) — all handler capabilities + approve/reject reserves up to $100,000, manage claim status transitions
- **Claims Manager** (`manager`) — all supervisor capabilities + approve/reject reserves up to $10,000,000, set override flag for claims exceeding the aggregate limit

Role enforcement is required both backend (approval endpoints validate caller role) and frontend (approve/reject buttons only rendered for supervisor/manager).

## 6. Deliverables (Assessment §4)

1. Source code repository: backend (Clean Architecture), Angular frontend, EF Core migrations, SQL seed scripts, optional docker-compose, CI/CD pipeline definition, `README.md`
2. `README.md` — prerequisites, local setup, environment variables, migration/seeding steps, Azure deployment description, feature walkthrough
3. Deployed Azure application — public backend URL (Swagger accessible) + public frontend URL + at least 2 test accounts (handler, supervisor)
4. `ARCHITECTURE.md` — solution structure, data model, CQRS flow, domain events, Hangfire job design, Azure architecture, key decisions/tradeoffs
5. `AI-WORKFLOW.md` — tools used, workflow structure, ≥3 representative prompts, what was AI-generated vs. manually designed, ≥2 examples of AI errors and corrections, honest assessment, link to AI interaction history
6. AI interaction history export (Claude/Cursor/ChatGPT)

## 7. Evaluation criteria (Assessment §6)

| Area | Weight |
|---|---|
| Engineering Quality & Architecture | 35% |
| AI-Assisted Delivery Effectiveness | 25% |
| Fullstack Ownership & Delivery | 25% |
| Domain & Product Thinking | 15% |

Scope management guidance if time-constrained (Assessment §8.3): prioritize (1) backend completeness/correctness, (2) deployed Azure application, (3) architecture documentation, over a fully-polished frontend.

## 8. Live technical review (Assessment §7)

90-minute session: architecture walkthrough (20 min), AI workflow review (15 min), feature demonstration (20 min), deep-dive questions (20 min), live implementation task (15 min). Candidate must be able to explain every implementation decision.

## 9. Open Questions / Conflicts Between Source Documents

These are genuine discrepancies between the FRS and the Assessment Brief, or gaps neither document resolves. They are **not** resolved here — see `docs/business-rules.md` and `docs/domain-model.md` for the working assumption adopted in each case, called out explicitly as an assumption.

1. ~~**Closure path rigidity.**~~ **RESOLVED — see `docs/decisions.md` ADR-001.** FRS §4.2 permits `Open → Closed` and `UnderInvestigation → Closed` directly, gated only by the closure conditions in §4.3. Assessment §3.4.1 (BR-C-06) states "a draft claim may not transition directly to Closed. Required path: Draft → Open → UnderInvestigation → PendingPayment → Closed (or Draft → Open → Closed for trivial claims, if configured)" — implying a forced, sequential path. Decision: follow the FRS's explicit table as-is.
2. **Status-transition data model.** Assessment §3.2 lists a `ClaimStatusTransitions` entity (FromStatus, ToStatus, RequiredPermission) as a data-driven transition table. The FRS never defines such a table — it specifies the state machine as a fixed rule set in §4.2/§7.3, implying application-code enforcement rather than a DB-driven table. The FRS's detailed per-transition rule table is the more specific and complete source; see `docs/architecture.md` for the assumption adopted.
3. **Reserve component naming/set.** FRS §6.2 and §9.5 define components as `Indemnity`, `Expense`, `ALAE`, `SubrogationRecoverable`. Assessment Appendix A.2 (explicitly a "condensed reference" per its own heading) lists `IndemnityReserve`, `ExpenseReserve`, `RecoveryReserve`, `LitigationReserve` — different names, folds ALAE into Expense, and adds a `LitigationReserve` type not present anywhere in the FRS. Treated as a naming inconsistency in a summary appendix, not a real extra requirement.
4. ~~**Reserve adjustment endpoint shape.**~~ **RESOLVED — see `docs/decisions.md` ADR-002.** FRS §10.2 uses a single `POST /api/claims/{id}/reserves` for both opening and adjusting a reserve (disambiguated by a `transactionType` field). Assessment §3.3.3 additionally lists `PUT /api/claims/{id}/reserves/{reserveId}` for "Adjust reserve amount." Given reserves are explicitly event-sourced / append-only (FRS §6.6, "you do not UPDATE a reserve record"), a `PUT` that implies mutation is arguably incompatible with the FRS's own data model. Decision: expose both — `POST` opens, `PUT` adjusts — with `PUT` implemented as an internal `INSERT`, never an `UPDATE`.
5. **Policy coverage endpoint vs. out-of-scope coverage evaluation.** Assessment §3.3.2 requests `GET /api/policies/{id}/coverage`. FRS §2 explicitly marks "Coverage Evaluation" out of scope and treats coverage types as seed-only reference data returned alongside policy search results (FRS §5.5, §5.2 Step 1). No dedicated coverage endpoint is defined in the FRS.
6. **`ClaimType` field.** Assessment §3.2 lists `ClaimType` as a "key field" of the Claims entity. The FRS's Claims schema (§9.1) has no `ClaimType` column, and no business rule anywhere references a claim type. Likely an artifact of the Assessment's condensed table rather than a real requirement.
7. **Severity field semantics.** FRS §9.1 defines `Severity` (Catastrophic / Critical / Standard / Minor) on the Claims entity, but no section of either document specifies how severity is determined, who sets it, or what depends on it. Left as an open question — no default or computation rule is invented here.
8. ~~**Manager override flag storage.**~~ **RESOLVED — see `docs/decisions.md` ADR-003.** BR-R-05 (FRS §7.2) requires a Manager to "set an override flag on the claim" when total reserves would exceed $10,000,000, but the Claims entity schema in FRS §9.1 has no corresponding column (no `OverrideFlag`, no who/when/why). Decision: `OverrideFlag`/`OverrideByUserId`/`OverrideAt`/`OverrideReason` added to `Claims` (aggregate-level, since the $10M check is claim-wide).
9. **"Manage claim status transitions" (role table) vs. per-transition rules (§4.2).** FRS §3 lists "manage claim status transitions" as a Supervisor-and-above capability, but §4.2's detailed transition table allows a Handler to perform most transitions unaccompanied (e.g., `Draft → Open`, `Open → UnderInvestigation`). Only `Closed → Reopened` explicitly requires Supervisor role (BR-ST-04). The two statements are not fully reconciled in the source.
10. **File size limit.** FRS §13 states "No hard limit specified in this assessment; implement a reasonable 50 MB limit" — this is the FRS's own suggested default, not a hard business requirement; flagged here only so it isn't mistaken for a strict constraint from the business.

The following items (11–19) were found during a documentation review pass on 2026-09-22, cross-checking all six generated documents against both source files:

11. **Handler assignment as an Open-transition condition.** FRS §4.1's status table lists Open's entry condition as "All critical validation passes; handler set," but the detailed transition table (§4.2) and BR-ST-02 (§7.3) only require (a) no Critical issues and (b) ≥1 Claimant — neither mentions handler assignment. Not reconciled in the source. `docs/business-rules.md` follows §4.2/BR-ST-02 (the more specific, enforceable rule) and does not treat handler assignment as blocking.
12. ~~**Reserve submission cap vs. approval-authority cap.**~~ **RESOLVED — see `docs/decisions.md` ADR-004.** FRS §3 says a Handler may "open reserves within authority (≤ $10,000)." This could mean the Handler is blocked from *submitting* above $10,000, or may submit any amount with the threshold only governing auto-approval vs. routing to `PendingApproval`. Decision: no additional submission-time cap; a Handler may submit any amount, and the threshold table only governs the resulting approval status.
13. **Dangling rule reference.** FRS §4.2's `Open → Closed` row cites "see BR-CL-01," but no rule with that ID exists anywhere in the FRS (closure conditions are the unlabeled CC-01..CC-04 in §4.3; the closure business rule is BR-ST-03 in §7.3). Treated as a broken cross-reference in the source; CC-01..CC-04 is taken as the intended target.
14. ~~**No audit event type for the Manager override flag.**~~ **RESOLVED — see `docs/decisions.md` ADR-003.** BR-R-05 requires a Manager to set an override flag when total approved reserves would exceed $10,000,000 — a significant action per BR-A-02's own standard — but FRS §14.1's enumerated EventType list has no corresponding entry. Decision: added `RESERVE_OVERRIDE_SET` event type.
15. **Single vs. dual audit entries on Closed/Reopened.** FRS §14.1 lists both a generic `STATUS_CHANGED` event and specific `CLAIM_CLOSED`/`CLAIM_REOPENED` events. Not specified whether closing a claim writes one entry or two.
16. **Tenant-isolation query filter not mandated.** FRS §15.2 requires a global EF Core query filter for soft delete but not an equivalent one for `OrganisationId`, despite §15.1 requiring the column on every business table. Since only one `OrganisationId` is ever seeded in this assessment, this has no observable effect here, but is worth a defense-in-depth filter regardless.
17. **Table-naming convention is self-contradictory in the source.** FRS §15.3 labels the table-naming convention "PascalCase singular noun" but its own examples (`Claims`, `LossEvents`, `ClaimParties`) are plural. `docs/architecture.md` follows the examples.
18. **Correlation ID propagation for background-job-originated audit entries.** FRS §14.2 requires `CorrelationId` propagation "from the HTTP request context," but `GL_POSTING_SIMULATED` and `SLA_BREACH_DETECTED` entries originate from Hangfire jobs with no HTTP request in scope. Not addressed by either source document.
19. **Whether the 422-validation-error pattern for authorization failures generalizes.** FRS §8 explicitly models two reserve-approval authorization checks (wrong role; self-approval) as Critical validation issues returned via the structured `422` body, not `401`/`403`. Never stated whether this extends to other role gates (e.g., BR-ST-04's Supervisor-only Reopen). `docs/business-rules.md` and `docs/api-contract.md` assume consistency (422 throughout) as the more defensible inference, but it is an inference, not a stated rule.

## 10. Corrections Made During Documentation Review (2026-09-22)

These were straightforward errors/omissions in the first pass, not source-document ambiguities — they've been corrected directly rather than logged as open questions:

- **Entity relationship bug:** `docs/domain-model.md` previously modeled Policy→Claim as 1-to-(0 or 1), wrongly implying a policy could have at most one claim. Corrected to 1-to-many; nothing in either source document supports a one-claim-per-policy limit.
- **Missing schema field:** `ClaimReserveComponents` is named explicitly in FRS §15.1 as requiring `RowVer` (optimistic concurrency) alongside `Claims`, but `docs/domain-model.md`'s field list for that entity omitted it. Added.
- **Missing concurrency detail:** FRS §5.3 specifies a concrete technique for atomic claim-number generation (`UPDATE ... OUTPUT INSERTED` / `SEQUENCE` object / sequence table with optimistic concurrency) — this was reduced to "generated atomically" in the first pass, losing the specified technique. Restored in `docs/business-rules.md` and consolidated in a new `docs/architecture.md` "Concurrency & Idempotency Requirements" section.
- **Missing validation rule:** FRS §5.4 lists "no risk objects linked" as an explicit Warning-severity example, which was omitted from the validation summary table in the first pass. Added to `docs/business-rules.md` §12.
- **Missing detailed UI requirements:** FRS §11.1–§11.3 (Claims List, FNOL form, Claim Detail tabs — table columns, badge colors, per-step form fields, tab contents) was captured only at a general-principles level (§11.4) in `docs/architecture.md`. The detailed screen-by-screen requirements have been added.
- **Internal inconsistency:** the GL posting idempotency key placeholder `{ReserveId}` was clarified as `ReserveComponentId` in `docs/domain-model.md` and `docs/business-rules.md` §5 (BR-R-06) but not in `docs/business-rules.md` §11.1 (job spec), where it was repeated unclarified. Now consistent.

No API endpoints were found in `docs/api-contract.md` that lack FRS support — all 17 documented endpoints trace directly to FRS §10.1–§10.3, and the two Assessment-only endpoints (reserve `PUT`, policy `/coverage`) were already correctly excluded and flagged rather than included.
