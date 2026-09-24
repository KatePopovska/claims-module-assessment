# API Contract

Source: FRS §10 (authoritative endpoint list and behavior), cross-checked against Assessment §3.3. General conventions: RESTful JSON, Bearer (JWT) auth, validation errors return `422 Unprocessable Entity` with the structured body in §5 below, idempotent writes where an `Idempotency-Key` header is supplied (FRS §10 intro).

## 1. Claims Endpoints (FRS §10.1)

| Endpoint | Method | Behavior |
|---|---|---|
| `/api/claims` | POST | Create a claim (FNOL). Validates all business rules. Generates `ClaimNumber` atomically. Creates `LossEvent`, `Parties`, `RiskObjects`, and optional initial reserve transactionally. Returns `201` with created claim incl. `ClaimNumber`. |
| `/api/claims` | GET | Paginated, filterable list. Query params: `status` (repeatable: `?status=Open&status=Closed`), `dateFrom`, `dateTo` (loss date, inclusive), `assignedHandlerId`, `causeOfLossCode`, `policyId`, `search` (partial claim number or client name), `page` (default 1), `pageSize` (default 20, max 100). Returns `{ items, totalCount, page, pageSize }`; each item: id, claimNumber, clientName, policyNumber, lossDate, causeOfLossCode, causeOfLossName, status, assignedHandlerId, totalReserves (sum of approved/auto-approved reserve transactions). Newest reported first. |
| `/api/claims/{id}` | GET | Full claim detail: header fields incl. `validNextStatuses`, loss event, parties (incl. inactive), risk objects, reserve summary (`totalReserves` + per component `currentBalance`/`pendingAmount`), document list (metadata only), 10 most recent audit log entries. `404` if not found. |
| `/api/claims/{id}/status` | PUT | Transition claim status. Body: `{ targetStatus, reason?, acknowledgeWarnings? }`. Validates against state machine; `422` with blocking conditions if not permitted. `reason` max 500 chars; required for `Withdrawn`, `Reopened`, and `Closed` when any reserve has a non-zero approved balance. `acknowledgeWarnings` (default `false`) must be `true` for `Draft → Open` when the loss date is outside the policy effective period (BR-C-02). Every transition is role-checked (handler/supervisor/manager; unknown role → `422`). For `targetStatus = Reopened`, caller must have Supervisor or Manager role (BR-ST-04) — whether a role failure here returns `422` (consistent with the reserve-approval pattern) or `403` is not stated by the FRS; see requirements.md §9.19. |
| `/api/claims/{id}/audit` | GET | Paginated audit log, reverse-chronological. Query params `page` (default 1), `pageSize` (default 20, max 100). Returns `{ items, totalCount, page, pageSize }`. `404` if the claim doesn't exist. |
| `/api/claims/{id}/parties` | POST | Add a party to the claim. Body: `{ partyRole, partyType, firstName?, lastName?, companyName?, email?, phone?, notes? }`. Person requires first and last name; Company requires company name. Returns `201` with the created party. `404` if the claim doesn't exist. |
| `/api/claims/{id}/parties/{partyId}` | DELETE | Soft-remove a party (`IsActive = false`). `422` if removing the last active Claimant. Returns `204`; removing an already-inactive party is a no-op `204` (no second audit entry). `404` if the claim or party doesn't exist. |
| `/api/claims/{id}/documents` | POST | Upload a document (multipart). Stores in Azure Blob Storage. Creates `ClaimDocuments` record. |
| `/api/claims/{id}/documents` | GET | List documents; each includes a short-lived SAS download URL (1-hour TTL). |

## 2. Reserve Endpoints (FRS §10.2)

| Endpoint | Method | Behavior |
|---|---|---|
| `/api/claims/{id}/reserves` | POST | Opens a **new** reserve component (first transaction for a given `Component` type on the claim; `TransactionType = Add`). Body: `{ component, amount, changeReason }`. Requires the claim to have a linked policy (`PolicyId` not null) — BR-C-06 blocks all reserve creation while unlinked. Validates authority threshold. Creates `ReserveHistory` record. If auto-approved, enqueues GL posting job. Returns `201` with the created transaction and its approval status. |
| `/api/claims/{id}/reserves/{reserveComponentId}` | PUT | Adjusts an **existing** reserve component's balance. Body: `{ amount, changeReason }` (delta, positive or negative). Requires the claim to have a linked policy. Internally **inserts** a new `ReserveHistory` row (`TransactionType = Adjust`/`Reverse`, `ChangeSequence` incremented) — never mutates an existing history row (FRS §6.6). Goes through the same authority-threshold/approval workflow as `POST`. `ClaimReserveComponents.CurrentAmount` is recomputed, not directly assigned. See `docs/decisions.md` ADR-002. |
| `/api/claims/{id}/reserves` | GET | Reserve summary (current balance per component) + full transaction history. |
| `/api/claims/{id}/reserves/{txnId}/approve` | POST | Approve a pending reserve. Requires Supervisor/Manager role. Validates role, validates not self-approving. On success: updates approval status, enqueues GL posting job. |
| `/api/claims/{id}/reserves/{txnId}/reject` | POST | Reject a pending reserve. Requires Supervisor/Manager. Body: `{ rejectionReason }`. Sets transaction status to `Rejected`. |
| `/api/claims/{id}/reserves/{txnId}/retract` | POST | Submitter retracts their own pending reserve before approval. Sets status to `Cancelled`. |

> **Resolved (docs/decisions.md ADR-002):** the FRS's single-`POST` design and the Assessment Brief's `PUT` requirement are both honored — `POST` opens a component, `PUT` adjusts one, and both write only `INSERT`s to `ReserveHistory` internally. See the table above.
>
> **Resolved (docs/decisions.md ADR-004):** a Handler may submit or adjust a reserve of any amount; the amount only determines the resulting `ApprovalStatus`, not whether the submission is accepted. No per-transaction role cap is enforced beyond the threshold table in `docs/business-rules.md` §6.
>
> **Note (authorization error modeling, see requirements.md §9.19):** FRS §8 explicitly returns the two reserve-approval role checks (wrong role; self-approval) as `422` validation errors, not `401`/`403`. This contract follows that pattern for the `/approve` and `/reject` endpoints below.

## 3. Reference Data Endpoints (FRS §10.3)

| Endpoint | Method | Behavior |
|---|---|---|
| `/api/reference/cause-of-loss-codes` | GET | List active cause-of-loss codes. Optional filter `?perilCategory={category}`. |
| `/api/reference/claim-statuses` | GET | List all claim status values with valid next-status transitions. |
| `/api/policies/search` | GET | Search simulated policies. Query param `q` (policy number or client name). Returns matching policies incl. effectiveDate, expirationDate, status, coverageTypes. |

> **Note (open question, see requirements.md §9.5):** Assessment §3.3.2 additionally requests `GET /api/policies/{id}/coverage`. The FRS treats coverage as seed-only reference data returned as part of policy search results (FRS §5.5) and explicitly marks "Coverage Evaluation" out of scope (FRS §2). No dedicated coverage endpoint is defined in the FRS. Not included in this contract pending clarification of whether it's needed for the Step-1 "available coverage types" display (FRS §5.2), which may just reuse the `coverageTypes` field already returned by policy search.

## 4. Error Response Structure (FRS §10.4)

All error responses follow this shape:

```json
{
  "type": "ValidationError",
  "title": "One or more validation errors occurred.",
  "status": 422,
  "errors": {
    "LossDate": ["Loss date cannot be in the future."],
    "ClaimParties": ["At least one Claimant party is required."]
  }
}
```

## 5. Cross-Cutting API Requirements

- Authentication: Bearer token (JWT); mock/simulated auth service acceptable (FRS §3, Assessment §3.7.4)
- Authorization: reserve approval endpoints must validate caller role server-side (FRS §3) — not just hide UI controls
- Idempotency: write endpoints support an `Idempotency-Key` request header (FRS §10 intro)
- All endpoints are RESTful JSON; kebab-case plural route nouns (FRS §15.3)
- No endpoint proxies document bytes — document retrieval always returns a SAS/local URL, never streams the file through the API (FRS BR-D-02)

## 6. Field-Level Validation Reference

See `docs/business-rules.md` §12 for the full field/rule/severity table that FluentValidation validators must implement for claim and reserve commands.
