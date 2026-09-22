# Decisions

This file records decisions made by the project owner to resolve ambiguities and conflicts identified during requirements review (see `docs/requirements.md` §9). Each entry is authoritative for implementation going forward — where a decision here conflicts with a literal reading of either source document, **this file wins**.

Format follows a lightweight ADR (Architecture/business rule Decision Record) style: Context, Decision, Consequences.

---

## ADR-001 — Status Transitions Follow the FRS's Explicit Table

**Resolves:** `docs/requirements.md` §9 item 1 (closure-path rigidity)
**Status:** Accepted — 2026-09-22

**Context:** The FRS's transition table (§4.2) permits `Open → Closed` and `UnderInvestigation → Closed` directly, gated only by the closure conditions in §4.3. The Assessment Brief's BR-C-06 separately implies a forced sequential path (`Draft → Open → UnderInvestigation → PendingPayment → Closed`).

**Decision:** Implement the state machine exactly as already documented in `docs/business-rules.md` §2 (the FRS's explicit table). No forced sequential path is added. This is not treated as a blocker for scaffolding or domain modeling — the transition rules are ordinary application/domain logic (a rule table + validator), not a structural constraint on the schema, so they can be revised later without an architectural change if needed.

**Consequences:** No changes required to `docs/business-rules.md` or `docs/domain-model.md`. The status-transition rule set will be implemented as an explicit, testable rule table in the Domain/Application layer (see `docs/architecture.md` §9's note on data-driven vs. code-driven enforcement — either storage approach must reproduce this exact rule table).

---

## ADR-002 — Reserve Adjustment: PUT Externally, Event-Sourced Insert Internally

**Resolves:** `docs/requirements.md` §9 item 4 (reserve-adjustment endpoint shape)
**Status:** Accepted — 2026-09-22

**Context:** The FRS requires reserves to be strictly append-only/event-sourced (§6.6: "you do not UPDATE a reserve record... The current balance is computed as the sum of all posted/approved transactions"). The FRS's own API design reflects this with a single `POST` for both opening and adjusting a reserve. The Assessment Brief instead specifies a `PUT /api/claims/{id}/reserves/{reserveId}` endpoint for "adjust reserve amount." These aren't reconcilable at face value — a `PUT` implies replacing a resource; the FRS explicitly forbids that at the data layer.

**Decision:** Keep the `PUT` endpoint as the **external API contract** (satisfies the Assessment Brief), but never let it perform a SQL `UPDATE` on a `ReserveHistory` row:

- `POST /api/claims/{id}/reserves` — opens a **new** reserve component (the first transaction for a given `Component` type on a claim). `TransactionType = Add`.
- `PUT /api/claims/{id}/reserves/{reserveComponentId}` — adjusts an **existing** reserve component's balance. The `{reserveComponentId}` path segment addresses `ClaimReserveComponents.ReserveComponentId` — it does not address a `ReserveHistory` row, and there is no `ReserveHistory` row ID in the URL, because there's nothing there to target for an update.
- Internally, the `PUT` handler **inserts** a new `ReserveHistory` row (`TransactionType = Adjust`, or `Reverse` for a balance-reducing change), computing `PreviousBalance`/`NewBalance` from the existing history and incrementing `ChangeSequence`. It goes through the exact same authority-threshold and approval workflow as `POST` (FRS §6.3/§6.4) — an adjustment above $10,000 still routes to `PendingApproval` just like an initial reserve would.
- `ClaimReserveComponents.CurrentAmount` remains a derived value, recomputed (not directly assigned) after each accepted history row.

**Consequences:**
- `docs/api-contract.md` §2 gets a new `PUT` row (previously excluded pending this decision).
- The FRS's `transactionType` request-body field becomes implicit on `POST` (always `Add`) rather than client-supplied; `PUT` implies `Adjust`/`Reverse` based on the sign of the delta.
- No change to `docs/domain-model.md`'s `ReserveHistory` entity — it already models exactly this event-sourced shape.

---

## ADR-003 — Manager Override Flag Stored on Claims (Aggregate-Level)

**Resolves:** `docs/requirements.md` §9 item 8 (override flag storage) and contributes to item 14 (missing audit event for the override action)
**Status:** Accepted — 2026-09-22

**Context:** BR-R-05 requires a Manager to set an override flag when total *approved* reserves across all components on a claim would exceed $10,000,000. The FRS's Claims schema (§9.1) never defines a column for this.

**Decision:** The $10,000,000 threshold in BR-R-05 is evaluated as an aggregate across the whole claim (sum of approved reserves across *all* components), not per-component — so the override flag is stored on `Claims`, not on `ClaimReserveComponents`. Add to the `Claims` entity:

| Column | Type | Notes |
|---|---|---|
| `OverrideFlag` | `BIT NOT NULL DEFAULT 0` | true once a Manager has authorized exceeding the $10M aggregate limit |
| `OverrideByUserId` | `UNIQUEIDENTIFIER NULL` | Manager who set it |
| `OverrideAt` | `DATETIMEOFFSET(7) NULL` | when it was set |
| `OverrideReason` | `NVARCHAR(500) NULL` | justification |

Setting this flag is a significant business action per BR-A-02's general principle ("every significant business action... must create an audit log entry"), so it must be logged. A new `EventType` value, `RESERVE_OVERRIDE_SET`, is added to the audit event enumeration to cover it (fills the gap noted in `docs/requirements.md` §9 item 14 — the FRS's own §14.1 list never anticipated this event).

**Consequences:**
- `docs/domain-model.md` §3.1 (Claims) updated: the four columns above replace the previous "not defined in FRS schema" callout.
- `docs/business-rules.md` §10 (audit events) gets the new `RESERVE_OVERRIDE_SET` event type.
- A reserve transaction that would push the claim's aggregate approved total over $10M must be blocked from `Approved` status until `Claims.OverrideFlag = true`; setting the flag is itself a Manager-only action (mirrors the BR-ST-04 Reopen pattern of a role-gated side-effect on the Claim).

---

## ADR-004 — No Additional Per-Transaction Reserve Cap

**Resolves:** `docs/requirements.md` §9 item 12 (reserve submission-cap ambiguity)
**Status:** Accepted — 2026-09-22

**Context:** FRS §3's role table describes a Handler's authority as "open reserves within authority (≤ $10,000)," which could be read either as a hard submission-time cap or as just describing what auto-approves.

**Decision:** Implement only the authority thresholds explicitly stated in FRS §6.3/BR-R-02:

- ≤ $10,000 → auto-approved, any role may submit
- \> $10,000 and ≤ $100,000 → created `PendingApproval`, requires Supervisor or Manager to approve
- \> $100,000 → created `PendingApproval`, requires Manager to approve

**A Handler may submit a reserve transaction of any amount.** The amount determines the resulting `ApprovalStatus` (`Approved` vs. `PendingApproval`), not whether the submission itself is accepted. No submission-time role gate is added beyond what the FRS states — do not invent an additional per-transaction maximum for any role.

The separate $10,000,000 rule (BR-R-05) is an **aggregate, claim-level** limit — evaluated by summing approved reserves across all of a claim's components — and is resolved via the Manager override flag (ADR-003), not a per-transaction cap.

**Consequences:**
- `docs/business-rules.md` §5's "Missing authorization rule (open question)" callout is superseded by this decision.
- `docs/api-contract.md` §2's corresponding note is superseded by this decision.
- The reserve-creation/adjustment command handler validates only: amount > 0 (except SubrogationRecoverable), resulting `ApprovalStatus` per the threshold table above, and — separately — the aggregate $10M check against `Claims.OverrideFlag`.

---

## Status of Related Open Questions

| requirements.md §9 item | Status |
|---|---|
| 1 — Closure-path rigidity | **Resolved** — ADR-001 |
| 4 — Reserve-adjustment endpoint shape | **Resolved** — ADR-002 |
| 8 — Manager override flag storage | **Resolved** — ADR-003 |
| 12 — Reserve submission-cap ambiguity | **Resolved** — ADR-004 |
| 14 — Missing audit event for override flag | **Resolved** (partially — event type named) — ADR-003 |
| all others | Still open; not blocking scaffolding or the domain model |
