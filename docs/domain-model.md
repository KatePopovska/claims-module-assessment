# Domain Model

Source: FRS §1.2 (domain concepts), §9 (data entities), Assessment §3.2. This describes the *entities and relationships required by the requirements*, normalized from both documents. It is not a finalized EF Core schema — that belongs to implementation.

## 1. Core Domain Concepts (FRS §1.2)

- **Policy** — a contract between an insurer and a policyholder; has effective/expiration dates; covers one or more insured assets against specific loss types. *Simulated* in this assessment (seeded data, not a real Policy module — FRS §5.5, Assessment §3.3.2).
- **Loss Event** — the incident causing damage/injury: date, location, cause. One loss event may give rise to one claim.
- **Claim** — the formal record created from a loss event; links the loss event to the policy and affected parties; tracks financial liability (reserves); drives investigation and payment.
- **Reserve** — a financial estimate of expected claim cost; set at intake, adjusted over the claim's life; linked to a cost component; approval-gated above threshold.
- **Cause of Loss Code** — standardized peril code (fire, theft, collision, flood, etc.).
- **Claimant** — the party seeking compensation (insured, third party, or beneficiary).

## 2. Entity List and Relationships

```
Policy (simulated) 0..1───0..* Claim        (a policy may have many claims over its life; a claim has 0 or 1 policy)
Claim  1───1        LossEvent
Claim  1───0..*      ClaimParty
Claim  1───0..*      ClaimRiskObject
Claim  1───0..*      ClaimReserveComponent
ClaimReserveComponent 1───0..* ReserveHistory
Claim  1───0..*      ClaimDocument
Claim  1───0..*      ClaimAuditLog
CauseOfLossCode 1───0..* LossEvent
```

> **Correction (review pass, 2026-09-22):** the Policy↔Claim relationship was previously mis-documented as 1-to-(0 or 1), which would wrongly forbid a policy from having more than one claim over its life. Nothing in either source document supports that constraint — corrected to 1-to-many from the Policy side.

Aggregate root: **Claim**. Everything else (`LossEvent`, `ClaimParty`, `ClaimRiskObject`, `ClaimReserveComponent`, `ClaimDocument`, `ClaimAuditLog`) is a child entity scoped to a claim. `ReserveHistory` is a child of `ClaimReserveComponent`. `Policy` and `CauseOfLossCode` are independent reference/simulated entities referenced by `Claim`/`LossEvent` respectively.

## 3. Entities (FRS §9)

### 3.1 Claims (aggregate root)

| Field | Type | Notes |
|---|---|---|
| ClaimId | GUID | PK |
| OrganisationId | GUID | tenant isolation, NOT NULL |
| ClaimNumber | NVARCHAR(50) | system-generated, unique per org, format `CLM-{YYYY}-{7-digit-seq}` |
| PolicyId | GUID, nullable | FK to simulated Policy; null = unknown-policy intake |
| PolicyNumber | denormalised | for display |
| ClientName | denormalised | for display |
| Status | NVARCHAR(50) | see lifecycle in `docs/business-rules.md` |
| Severity | NVARCHAR(50) | Catastrophic / Critical / Standard / Minor — **open question**: no rule defines who sets this or how (see requirements.md §9.7) |
| ReportedDate | DATETIMEOFFSET(7) | when FNOL was received |
| AssignedHandlerId | GUID, nullable | |
| ClosedAt | DATETIMEOFFSET(7), nullable | |
| ClosureReason | nullable | |
| Notes | NVARCHAR(MAX), nullable | |
| IsDeleted / audit columns / RowVer | — | see `docs/architecture.md` conventions |
| ReserveLimitOverride | BIT NOT NULL DEFAULT 0 | Manager override for the $10M aggregate reserve limit — see `docs/decisions.md` ADR-003 |
| ReserveLimitOverrideByUserId | GUID, nullable | Manager who set the override |
| ReserveLimitOverrideAt | DATETIMEOFFSET(7), nullable | when the override was set |
| ReserveLimitOverrideReason | NVARCHAR(500), nullable | justification |

### 3.2 LossEvents

LossEventId (PK), ClaimId (FK), LossDate, LossDescription (≥20 chars, required), LossLocation (free text, nullable), CauseOfLossCode (string FK → `CauseOfLossCodes.Code`, NVARCHAR(50), NOT NULL — FRS §9.2; not a GUID FK to `CauseOfLossCodeId`), EstimatedLossAmount (nullable), ReportDate, PoliceReportNumber (nullable).

### 3.3 ClaimParties

ClaimPartyId (PK), ClaimId (FK), PartyRole (`Claimant` / `Insured` / `ThirdParty` / `Witness` / `Attorney`), PartyType (`Person` / `Company`), FirstName, LastName, CompanyName, Email, Phone, Notes, IsActive (soft-remove flag).

Constraint: a claim may have multiple parties of the same role. At least one active `Claimant` is required before `Open` transition and must never drop to zero while active (FRS BR-P-01, API rule on party removal).

### 3.4 ClaimRiskObjects

ClaimRiskObjectId (PK), ClaimId (FK), AssetType (`Vehicle` / `Property` / `Person` / `Equipment` / `Other`), AssetDescription, DamageDescription (nullable), IsPrimary (bit), AssetReference (nullable — VIN, address, etc.).

A claim with zero risk objects is permitted but generates a Warning validation issue ("no risk objects linked" — FRS §5.4's Warning examples row); it does not block Draft→Open. See `docs/business-rules.md` §12.

### 3.5 ClaimReserveComponents

ReserveComponentId (PK), ClaimId (FK), Component (`Indemnity` / `Expense` / `ALAE` / `SubrogationRecoverable` — see requirements.md §9.3 for naming conflict with Assessment Appendix A.2), CurrentAmount (**computed**, sum of posted/approved `ReserveHistory` rows for this component), Status (`Active` / `Closed`), Notes (nullable), **RowVer** (`ROWVERSION`, optimistic concurrency — FRS §15.1 names this table explicitly, alongside Claims, as an aggregate root requiring RowVer).

Only `SubrogationRecoverable` may hold a negative amount; the other three must be > 0 (FRS BR-R-01, §6.2).

**Assumption:** the FRS's examples (§6.2, "an Indemnity reserve of $25,000, an Expense reserve of $5,000...") imply exactly one `ClaimReserveComponents` row per (ClaimId, Component) pair, with all changes to that component appended to `ReserveHistory` rather than creating a second row of the same component type. Not stated explicitly as a uniqueness constraint in the FRS, but assumed here since it's the only reading consistent with "current value is computed from the history table" (§9.5).

### 3.6 ReserveHistory

Append-only. ReserveHistoryId (PK), ReserveComponentId (FK), ClaimId (denormalised FK), TransactionType (`Add` / `Adjust` / `Reverse`), Amount (delta), PreviousBalance, NewBalance, ApprovalStatus (`AutoApproved` / `PendingApproval` / `Approved` / `Rejected` / `Cancelled`), ApprovedByUserId/ApprovedAt (nullable), RejectedByUserId/RejectedAt/RejectionReason (nullable), ChangeReason, PostingStatus (`Pending` / `Posted` / `Failed` / `Cancelled`), PostingJobId (nullable), IdempotencyKey (`Reserve:{ReserveComponentId}:Change:{ChangeSequence}` — the FRS's `{ReserveId}` placeholder is read as referring to `ReserveComponentId`, the only reserve-level identifier defined), ChangeSequence (monotonic per component), SubmittedByUserId, CreatedAt.

**Design rule (FRS §6.6, IMPORTANT box):** reserves are event-sourced. No `UPDATE` on a reserve record — every change is an `INSERT` into `ReserveHistory`; `ClaimReserveComponents.CurrentAmount` is a derived/computed value.

### 3.7 ClaimDocuments

ClaimDocumentId (PK), ClaimId (FK), DocumentType (`PoliceReport` / `MedicalReport` / `Invoice` / `Other`, free-form beyond these examples), DocumentName, BlobPath, ContentType, FileSizeBytes, UploadedAt, UploadedByUserId (nullable), Notes (nullable).

### 3.8 ClaimAuditLog

Append-only, immutable — no UPDATE/DELETE ever (BR-A-01). AuditLogId (PK), ClaimId (FK), EventType (enumerated set — see `docs/business-rules.md` §Audit), Description, OldValue/NewValue (JSON, nullable), RelatedEntityId/RelatedEntityType (nullable), CorrelationId (nullable, propagated from request context), CreatedAt, CreatedByUserId (nullable — system actor for background jobs).

### 3.9 CauseOfLossCodes (reference data)

CauseOfLossCodeId (PK), Code (unique — this is the alternate key LossEvents.CauseOfLossCode FKs to, not CauseOfLossCodeId), Name, PerilCategory (`Property` / `Auto` / `Liability` / `Weather` / `Equipment` / `Crime` / `General`), IsActive, SortOrder. Seeded set defined in FRS §5.6 (10 codes).

### 3.10 Policies (simulated)

PolicyId (PK), PolicyNumber (unique), ClientName, EffectiveDate, ExpirationDate, Status (`Active` / `Expired` / `Cancelled`), CoverageTypes (comma-separated or JSON array). Seeded set defined in FRS §5.5 (5 policies, including one intentionally expired policy `POL-2023-000099` for testing the out-of-period warning).

## 4. Reference Data Required at Seed Time

- 5 simulated policies (FRS §5.5)
- 10 cause-of-loss codes (FRS §5.6)
- A single fixed `OrganisationId` GUID (FRS §15.1, "seed your implementation with a single fixed OrganisationId")

## 5. Not Modeled (Explicitly Out of Scope)

Coverage evaluation logic, claim payments/disbursement, subrogation workflow (beyond the reserve component type), litigation case management, fraud/SIU scoring, bulk operation entities, messaging/communication threads, reinsurance — see requirements.md §3.
