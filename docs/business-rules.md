# Business Rules

Source: FRS §4, §6, §7, §8, §12, §14. Rule IDs are preserved from the FRS. Where the Assessment Brief (§3.4) states the same rule with different wording, it is noted inline; where it conflicts, see requirements.md §9.

## 1. Claim Status Lifecycle (FRS §4.1)

| Status | Meaning | Entry Condition |
|---|---|---|
| Draft | Created but incomplete/unvalidated | Automatic on first save |
| Open | Validated; handler assigned; active investigation | All critical validation passes; handler set |
| UnderInvestigation | Active detailed investigation | Handler manually transitions from Open |
| PendingPayment | Liability accepted; awaiting payment | Handler transitions; ≥1 reserve approved |
| Closed | All activity complete; financials reconciled | All closure conditions met (§4.3 below) |
| Reopened | Previously closed, reactivated with reason | Supervisor initiates with reason code |
| Withdrawn | Claimant withdrew | Handler transitions; reason recorded |

## 2. Valid Status Transitions (FRS §4.2, BR-ST-01)

Any transition not in this table is invalid and **must return HTTP 422**.

| From | To | Rule |
|---|---|---|
| Draft | Open | No unresolved Critical validation issues; ≥1 Claimant party exists |
| Open | UnderInvestigation | Handler or Supervisor; no additional conditions |
| Open | PendingPayment | ≥1 reserve component with status `Approved` |
| Open | Closed | All closure conditions satisfied (§3 below)¹ |
| Open | Withdrawn | Withdrawal reason code provided |
| UnderInvestigation | Open | Handler manually reverts |
| UnderInvestigation | PendingPayment | ≥1 approved reserve; liability determined |
| UnderInvestigation | Closed | All closure conditions satisfied |
| UnderInvestigation | Withdrawn | Withdrawal reason provided |
| PendingPayment | Closed | All closure conditions satisfied |
| Closed | Reopened | Reopen reason code provided; **Supervisor role required** (BR-ST-04) |
| Reopened | Open | Immediate, automatic |

> **Conflict flag:** Assessment BR-C-06 asserts a stricter, forced sequential path (`Draft → Open → UnderInvestigation → PendingPayment → Closed`) that is not supported by the table above, which allows `Open → Closed` and `UnderInvestigation → Closed` directly. This spec (docs/business-rules.md) follows the FRS's explicit transition table, since it is the more detailed and complete source for this rule. Flagged as an open question in requirements.md §9.1 — confirm before enforcing the stricter path.
>
> ¹ The FRS's own `Open → Closed` row cites "see BR-CL-01," but no rule with that ID exists anywhere in the FRS (closure conditions are the unlabeled CC-01..CC-04 in §4.3; the closure business rule is BR-ST-03 in §7.3). Treated as a broken cross-reference in the source; CC-01..CC-04 below is taken as the intended target (requirements.md §9.13).
>
> **Note:** FRS §4.1's status table describes Open's entry condition as "All critical validation passes; handler set," implying handler assignment is required to reach Open. Neither the transition table above nor BR-ST-02 (§7 below) lists handler assignment as a condition — only "no Critical issues" and "≥1 Claimant." Not reconciled in the source; this spec follows the more specific/enforceable §4.2/BR-ST-02 wording and does not treat handler assignment as a blocking condition for Open (requirements.md §9.11).

## 3. Closure Conditions (FRS §4.3, BR-ST-03)

A claim may transition to `Closed` only when ALL of the following hold. The API must return the list of failing conditions on 422.

- **CC-01** — No reserve components with status `PendingApproval` remain.
- **CC-02** — No unresolved Critical validation issues on the claim.
- **CC-03** — At least one `ClaimParty` with role `Claimant` exists.
- **CC-04** — If any reserve component has `CurrentAmount > 0` at closure time, this is a non-critical warning; the handler must explicitly confirm closure with open reserves via a justification note.

## 4. FNOL / Claim Creation Rules (FRS §7.1)

| ID | Rule | Severity |
|---|---|---|
| BR-C-01 | Loss date must not be in the future | Critical |
| BR-C-02 | Loss date must fall within the linked policy's effective/expiration dates; if outside, Warning "Loss date is outside the policy effective period" — does not block Draft creation, must be cleared/acknowledged before Open | Warning |
| BR-C-03 | ≥1 `ClaimParty` with role Claimant required before Draft→Open | Critical |
| BR-C-04 | Claim number unique per organisation, generated atomically; format `CLM-{YYYY}-{7-digit-zero-padded-sequence}`; soft-deleted claims still consume their sequence number. **Implementation technique is specified, not left open** (FRS §5.3): must use an atomic database-level counter increment (`UPDATE ... OUTPUT INSERTED` pattern or equivalent) to prevent duplicates under concurrent submissions; a database `SEQUENCE` object or a dedicated sequence table with optimistic concurrency are both acceptable. See `docs/architecture.md` §Concurrency & Idempotency Requirements. | — (structural / concurrency) |
| BR-C-05 | `CauseOfLossCode` must exist and be active in reference table | Critical |
| BR-C-06 | If no policy linked (`PolicyId` null): Warning "No policy linked — claim requires policy association before financial actions are permitted"; **reserve creation is blocked** until a policy is linked | Warning |
| BR-C-07 | Loss description required, minimum 20 characters | Critical |

**Previously missing from this table (review pass, 2026-09-22):** FRS §5.4's own Warning examples row explicitly lists "no risk objects linked" alongside "policy not found" and "loss date outside policy effective dates." This is a real Warning-severity validation issue, now added to the validation table in §12 below — it was omitted in the first pass.

Validation severities: **Critical** blocks Draft→Open transition; **Warning** does not block the transition but must be visible/acknowledged (FRS §5.4).

## 5. Reserve Rules (FRS §7.2)

| ID | Rule |
|---|---|
| BR-R-01 | Reserve transaction amount must be > 0, except `SubrogationRecoverable` which may be negative |
| BR-R-02 | Authority thresholds (see §6 below); GL posting job enqueued only after required approval is granted |
| BR-R-03 | A user may not approve their own reserve submission; compare submitter `userId` vs approver `userId`; reject with 422 "Self-approval is not permitted." |
| BR-R-04 | On rejection, submitter may create a new reserve transaction with revised amount; original rejected transaction remains in history as `Rejected` |
| BR-R-05 | Total approved reserves across all components on a claim must not exceed $10,000,000 (aggregate, claim-level check — not per-component); a new reserve that would breach this generates a Warning and requires a Manager to set an override flag on `Claims` before approval can proceed. Storage: `Claims.OverrideFlag`/`OverrideByUserId`/`OverrideAt`/`OverrideReason` — see `docs/decisions.md` ADR-003. |
| BR-R-06 | GL posting jobs use idempotency key `Reserve:{ReserveId}:Change:{ChangeSequence}` (read as `ReserveComponentId`, see domain-model.md §3.6); re-entrant safe — running twice produces no duplicate audit entries |

**Reserve lifecycle rule (FRS §6.4, not independently numbered):** a reserve in `PendingApproval` status may not be modified. To change the amount before approval, the submitter must first retract it (→ `Cancelled`), then submit a new transaction.

**Cross-reference:** BR-C-06 (§4 above) blocks all reserve creation while `PolicyId` is null ("reserve creation is blocked until a policy is linked"). This is a precondition on every `POST /api/claims/{id}/reserves` call, not just an FNOL-time concern — see `docs/api-contract.md` §2.

**Resolved (docs/decisions.md ADR-004):** a Handler may submit or adjust a reserve transaction of any amount; the amount only determines the resulting `ApprovalStatus` (`Approved` vs `PendingApproval`), not whether the submission is accepted. No per-transaction role cap beyond the threshold table in §6 is enforced.

## 6. Authority Thresholds (FRS §6.3, BR-R-02)

| Transaction Amount | Authority Required | System Behavior |
|---|---|---|
| ≤ $10,000 | Auto-approved (any role) | Reserve created `Approved`; GL posting job enqueued immediately |
| > $10,000 and ≤ $100,000 | Supervisor or Manager | Reserve created `PendingApproval`; no GL posting until approved |
| > $100,000 | Manager only | Reserve created `PendingApproval`; no GL posting until approved by Manager |

The threshold applies to the **individual transaction amount**, not the running total.

## 7. Status Transition Rules (FRS §7.3)

- **BR-ST-01** — Transitions must follow the state machine (§2 above); invalid target → HTTP 422 listing valid next statuses.
- **BR-ST-02** — Transition to `Open` requires: (a) no Critical validation issues, (b) ≥1 Claimant party.
- **BR-ST-03** — Transition to `Closed` requires all closure conditions (§3 above); 422 with blocking list if not met.
- **BR-ST-04** — Transition to `Reopened` requires caller has Supervisor role and provides a non-empty reopen reason; claim immediately transitions to `Open` after `Reopened`.

**Authorization error modeling (clarification):** FRS §8 explicitly models two reserve-approval role checks ("approver role must be Supervisor/Manager," "approver must not be the submitter") as **Critical validation issues** returned through the same structured `422` body as other business-rule violations — not as a `401`/`403`. The FRS never states whether this same 422-based pattern should extend to other role-gated actions, such as BR-ST-04's Supervisor-only Reopen. This spec assumes consistency (422 for all business-rule-based role gates) since that's the FRS's own explicit precedent, but this is an inference — see requirements.md §9.19.

## 8. Document Rules (FRS §7.4)

- **BR-D-01** — Stored in Azure Blob Storage at `claim-documents/{organisationId}/{claimId}/{filename}`; filename sanitized to remove path traversal characters.
- **BR-D-02** — Retrieval returns a short-lived SAS URL with 1-hour TTL; document bytes are never streamed through the API.
- **BR-D-03** — If Azure Blob Storage is not configured, fall back to local filesystem via a configurable `IStorageService` interface; provider selected via `appsettings.json`.
- MIME allowlist (FRS §13): PDF, JPEG, PNG, DOCX, XLSX, TXT, CSV. Files outside this list are rejected.
- Suggested max file size: 50 MB (FRS's own default, not a hard business constraint — see requirements.md §9.10).

## 9. Party Rules (FRS §7.5)

- **BR-P-01** — A claim must have ≥1 active Claimant before transitioning to Open (Critical validation issue if not).
- **BR-P-02** — Supported roles: Claimant, Insured, ThirdParty, Witness, Attorney. Multiple parties of the same role are allowed.
- Party removal (`DELETE /api/claims/{id}/parties/{partyId}`) is a soft-remove (`IsActive = false`) and must return 422 if it would remove the last active Claimant.

## 10. Audit Rules (FRS §7.6, §14)

- **BR-A-01** — `ClaimAuditLog` is append-only; no UPDATE/DELETE ever, enforced at the application layer. No update/delete permission granted at the DB/application level.
- **BR-A-02** — Every significant action must create an audit entry (see event list below).
- All writes go through a dedicated `IAuditLogService`; no direct `DbContext` writes to the audit table from handlers.
- All timestamps `DATETIMEOFFSET(7)`, stored UTC.
- `CorrelationId` from the HTTP request context propagates to all audit entries created within that request.

### Required Audit Events (FRS §14.1)

| EventType | Trigger |
|---|---|
| CLAIM_CREATED | New claim created via POST /api/claims |
| STATUS_CHANGED | Status transition applied (OldValue/NewValue = statuses) |
| PARTY_ADDED | New ClaimParty added |
| PARTY_REMOVED | ClaimParty soft-removed |
| RESERVE_CREATED | New reserve transaction submitted |
| RESERVE_AUTO_APPROVED | Reserve auto-approved (≤ $10,000) |
| RESERVE_APPROVED | Reserve manually approved |
| RESERVE_REJECTED | Reserve rejected (OldValue includes reason) |
| RESERVE_RETRACTED | Submitter retracted pending reserve |
| RESERVE_OVERRIDE_SET | Manager set the aggregate-limit override flag on the claim (docs/decisions.md ADR-003) — added in this spec; not in the FRS's original list |
| GL_POSTING_SIMULATED | GL job executed successfully (NewValue = journal details) |
| GL_POSTING_FAILED | GL job failed after all retries (NewValue = failure reason) |
| DOCUMENT_UPLOADED | Document uploaded (RelatedEntityId = documentId) |
| CLAIM_CLOSED | Claim transitioned to Closed (NewValue = closure reason) |
| CLAIM_REOPENED | Claim transitioned Closed→Reopened (NewValue = reopen reason) |
| SLA_BREACH_DETECTED | SLA job detected a claim not updated in 48 hours |
| VALIDATION_ISSUE_ADDED | New validation issue recorded against the claim |

**Gaps found in this list (review pass, 2026-09-22):**

- **No event type for the Manager override flag — resolved.** The FRS's own §14.1 list never anticipated BR-R-05's override action. `RESERVE_OVERRIDE_SET` has been added above per `docs/decisions.md` ADR-003.
- **Ambiguous double-firing on Closed/Reopened.** The FRS lists both a generic `STATUS_CHANGED` event and the specific `CLAIM_CLOSED`/`CLAIM_REOPENED` events. It does not say whether closing a claim writes one audit entry (`CLAIM_CLOSED` only) or two (`STATUS_CHANGED` + `CLAIM_CLOSED`). Left unresolved (requirements.md §9.15).
- **Correlation ID for background-job-originated entries.** §14.2 requires `CorrelationId` propagation "from the HTTP request context," but `GL_POSTING_SIMULATED` and `SLA_BREACH_DETECTED` entries originate from Hangfire jobs with no HTTP request in scope. Not addressed by either source document (requirements.md §9.18).

## 11. Background Jobs (FRS §12)

### 11.1 GL Posting Simulation Job (`PostGLReserveChangeJob`)

- Trigger: enqueued immediately (fire-and-forget, `BackgroundJob.Enqueue`) on any reserve approval (auto or manual)
- Input: `ReserveHistoryId`, `ClaimId`, `IdempotencyKey`
- Idempotency key: `Reserve:{ReserveId}:Change:{ChangeSequence}` (the FRS's `{ReserveId}` refers to `ReserveComponentId` — see domain-model.md §3.6)
- Behavior: check if the idempotency key already has `Posted` status in `ReserveHistory`; if yes, no-op; if no, write `ClaimAuditLog` entry (`GL_POSTING_SIMULATED`, describing `DR Change in Outstanding Reserves / CR Outstanding Loss Reserves`, amount = reserve amount), set `PostingStatus = Posted`, set `PostingJobId`
- Failure handling: on exception, Hangfire retries per default policy; after retries exhausted, set `PostingStatus = Failed` and write `GL_POSTING_FAILED` audit entry
- Must be re-entrant: check idempotency key before any write

### 11.2 SLA Monitoring Job (`SlaMonitoringJob`, recurring)

- Schedule: every 15 minutes (cron `*/15 * * * *`)
- Behavior: find claims with `Status IN (Draft, Open)` and `UpdatedAt < now - 48h`; for each, write `SLA_BREACH_DETECTED` audit entry ("Claim has not been updated in 48 hours"). **Does not change claim status.**
- Idempotency: only add a new breach entry if ≥24 hours have passed since the claim's last `SLA_BREACH_DETECTED` entry (no duplicate spam every 15 minutes)
- Does not send notifications; only writes to the audit log

> **Note:** Assessment §3.5 describes this job as flagging claims with "a SlaBreached status" — this conflicts with the FRS's explicit "Do not change claim status" instruction (§12.2). The FRS instruction is more specific and is followed here.

## 12. Validation Rules Summary Table (FRS §8)

| Field | Rule | Severity |
|---|---|---|
| LossDate | Must not be in the future | Critical |
| LossDate | Required / valid date | Critical |
| LossDate | If policy linked, must be within policy effective period | Warning |
| LossDescription | Required, ≥20 chars | Critical |
| CauseOfLossCode | Must exist and be active | Critical |
| PolicyId | If null, warning only | Warning |
| ClaimParties | ≥1 Claimant before Open | Critical |
| ClaimRiskObjects | Recommended: at least one risk object should be linked (FRS §5.2 Step 2, §5.4 Warning examples) | Warning |
| ReserveAmount | Must be > 0 (unless SubrogationRecoverable) | Critical |
| ReserveComponent | Must be a defined component type | Critical |
| ReserveAmount (aggregate) | Total across components ≤ $10M | Warning |
| StatusTransition | Must be a valid next status | Critical |
| StatusTransition (Closed) | All closure conditions satisfied | Critical |
| ReserveApproval | Approver role must be Supervisor/Manager | Critical |
| ReserveApproval | Approver must not be submitter | Critical |

These must be implemented as FluentValidation validators on the corresponding MediatR commands (FRS §8 intro).
