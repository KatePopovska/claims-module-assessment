# API Contract

Source: FRS §10 (authoritative endpoint list and behavior), cross-checked against Assessment §3.3. General conventions: RESTful JSON, Bearer (JWT) auth, validation errors return `422 Unprocessable Entity` with the structured body in §5 below, idempotent writes where an `Idempotency-Key` header is supplied (FRS §10 intro).

## 1. Claims Endpoints (FRS §10.1)

| Endpoint | Method | Behavior |
|---|---|---|
| `/api/claims` | POST | Create a claim (FNOL). Validates all business rules. Generates `ClaimNumber` atomically. Creates `LossEvent`, `Parties`, `RiskObjects`, and optional initial reserve transactionally. Optional body field `initialReserve: { component, amount, changeReason? }` — requires `policyId` ("No policy linked. Policy must be associated before reserves can be set."), same amount/threshold rules as `POST /reserves`. The claim is inserted first to obtain its database-generated id, then the reserve is recorded; both commit in one database transaction and roll back together on failure. An auto-approved initial reserve enqueues its GL posting job only after commit. Returns `201` with `{ id, claimNumber, status, reportedDate, initialReserve }`, where `initialReserve` is `{ transaction, warnings }` or `null`. |
| `/api/claims` | GET | Paginated, filterable list. Query params: `status` (repeatable: `?status=Open&status=Closed`), `dateFrom`, `dateTo` (loss date, inclusive), `assignedHandlerId`, `causeOfLossCode`, `policyId`, `search` (partial claim number or client name), `page` (default 1), `pageSize` (default 20, max 100). Returns `{ items, totalCount, page, pageSize }`; each item: id, claimNumber, clientName, policyNumber, lossDate, causeOfLossCode, causeOfLossName, status, assignedHandlerId, totalReserves (sum of approved/auto-approved reserve transactions). Newest reported first. |
| `/api/claims/{id}` | GET | Full claim detail: header fields incl. `validNextStatuses`, loss event, parties (incl. inactive), risk objects, reserve summary (`totalReserves` + per component `currentBalance`/`pendingAmount`), document list (metadata only), 10 most recent audit log entries. `404` if not found. |
| `/api/claims/{id}/status` | PUT | Transition claim status. Body: `{ targetStatus, reason?, acknowledgeWarnings? }`. Validates against state machine; `422` with blocking conditions if not permitted. `reason` max 500 chars; required for `Withdrawn`, `Reopened`, and `Closed` when any reserve has a non-zero approved balance. `acknowledgeWarnings` (default `false`) must be `true` for `Draft → Open` when the loss date is outside the policy effective period (BR-C-02). Every transition is role-checked (handler/supervisor/manager; unknown role → `422`). For `targetStatus = Reopened`, caller must have Supervisor or Manager role (BR-ST-04) — whether a role failure here returns `422` (consistent with the reserve-approval pattern) or `403` is not stated by the FRS; see requirements.md §9.19. |
| `/api/claims/{id}/notes` | PUT | Update the claim's free-text notes. Body: `{ notes }` (max 4000; blank clears them). Audited as `CLAIM_NOTES_UPDATED` with old/new values; unchanged notes are a no-op. Returns `204`. `404` if the claim doesn't exist. |
| `/api/claims/{id}/audit` | GET | Paginated audit log, reverse-chronological. Query params `page` (default 1), `pageSize` (default 20, max 100). Returns `{ items, totalCount, page, pageSize }`. `404` if the claim doesn't exist. |
| `/api/claims/{id}/parties` | POST | Add a party to the claim. Body: `{ partyRole, partyType, firstName?, lastName?, companyName?, email?, phone?, notes? }`. Person requires first and last name; Company requires company name. Returns `201` with the created party. `404` if the claim doesn't exist. |
| `/api/claims/{id}/parties/{partyId}` | DELETE | Soft-remove a party (`IsActive = false`). `422` if removing the last active Claimant. Returns `204`; removing an already-inactive party is a no-op `204` (no second audit entry). `404` if the claim or party doesn't exist. |
| `/api/claims/{id}/documents` | POST | Upload a document (multipart). Stores in Azure Blob Storage. Creates `ClaimDocuments` record. Form fields: `file` (required), `documentType?` (max 50, default `Other`), `notes?` (max 2000). Allowed: PDF, JPEG, PNG, DOCX, XLSX, TXT, CSV — the type is taken from the file extension, and PDF/JPEG/PNG/DOCX/XLSX content must match its file signature. Max 50 MB (`422` above it; requests over 55 MB are cut off by the server with a framework `400`). Stored at `claim-documents/{organisationId}/{claimId}/{documentId}-{sanitisedFilename}`. Audited as `DOCUMENT_UPLOADED` with `RelatedEntityId = documentId`. Returns `201` with the document and a download link. `404` if the claim doesn't exist. |
| `/api/claims/{id}/documents` | GET | List documents; each includes a short-lived SAS download URL (1-hour TTL). Each item: id, documentType, documentName, contentType, fileSizeBytes, uploadedAt, uploadedByUserId, notes, downloadUrl, downloadUrlExpiresAt. Azure: read-only SAS valid from 5 minutes ago to 1 hour ahead, HTTPS-only on HTTPS endpoints, served inline with the original file name. Local fallback (Development only): absolute `/uploads/...` URL served as static files, `downloadUrlExpiresAt = null`. Newest first. `404` if the claim doesn't exist. |

## 2. Reserve Endpoints (FRS §10.2)

| Endpoint | Method | Behavior |
|---|---|---|
| `/api/claims/{id}/reserves` | POST | Opens a **new** reserve component (first transaction for a given `Component` type on the claim; `TransactionType = Add`). Body: `{ component, amount, changeReason }`. Requires the claim to have a linked policy (`PolicyId` not null) — BR-C-06 blocks all reserve creation while unlinked. Validates authority threshold. Creates `ReserveHistory` record. If auto-approved, enqueues GL posting job. Returns `201` with the created transaction and its approval status. |
| `/api/claims/{id}/reserves/{reserveComponentId}` | PUT | Adjusts an **existing** reserve component's balance. Body: `{ amount, changeReason }` (delta, positive or negative). Requires the claim to have a linked policy. Internally **inserts** a new `ReserveHistory` row (`TransactionType = Adjust`/`Reverse`, `ChangeSequence` incremented) — never mutates an existing history row (FRS §6.6). Goes through the same authority-threshold/approval workflow as `POST`. `ClaimReserveComponents.CurrentAmount` is recomputed, not directly assigned. See `docs/decisions.md` ADR-002. |
| `/api/claims/{id}/reserves` | GET | Reserve summary (current balance per component) + full transaction history. Returns `{ totalReserves, reserveLimitOverride, components: [{ id, component, status, currentBalance, pendingAmount }], transactions: [...] }`, transactions newest first. `404` if the claim doesn't exist. |
| `/api/claims/{id}/reserves/{txnId}/approve` | POST | Approve a pending reserve. Requires Supervisor/Manager role. Validates role, validates not self-approving. On success: updates approval status, enqueues GL posting job. |
| `/api/claims/{id}/reserves/{txnId}/reject` | POST | Reject a pending reserve. Requires Supervisor/Manager. Body: `{ rejectionReason }`. Sets transaction status to `Rejected`. |
| `/api/claims/{id}/reserves/{txnId}/retract` | POST | Submitter retracts their own pending reserve before approval. Sets status to `Cancelled`. |
| `/api/claims/{id}/reserve-limit-override` | PUT | Manager sets the $10M aggregate-limit override (BR-R-05, ADR-003). Body: `{ reason }` (required, max 500). `422` for non-Manager or if already set. Returns `204`. Audited as `RESERVE_OVERRIDE_SET`. |

**Implemented behaviour (2026-09-24):**

- `POST`/`PUT` return `201` with `{ transaction, warnings }`. `transaction` carries `approvalStatus` (`AutoApproved` or `PendingApproval`), `previousBalance`/`newBalance`, `changeSequence` and `postingStatus`. `warnings` contains the BR-R-05 message when the change would take the claim's approved total over $10,000,000.
- Amount rules: `POST` requires `amount > 0` (non-zero for `SubrogationRecoverable`); `PUT` requires a non-zero delta and must not take a non-subrogation component's approved balance below zero. Max 19 digits / 4 decimals; `changeReason` optional, max 1000.
- Authority thresholds use the **absolute** transaction amount, so a large reduction needs the same approval as a large increase.
- While a component has a `PendingApproval` transaction, further `PUT`s on it return `422` until it's approved, rejected or retracted (FRS §6.4).
- A change that would breach the $10M limit is never auto-approved, and can't be approved until a Manager sets the override.
- Approve and reject both apply the amount authority (Supervisor up to $100,000, Manager above); approve also rejects self-approval. Only the submitter can retract. All three return `200` with the updated transaction, or `422` if it isn't `PendingApproval`.
- Concurrent edits of the same reserve return `409 Conflict`.
- No manual GL-posting retry endpoint is exposed: a failed posting is retried by the background job and surfaces as `postingStatus = Failed` (UI shows the badge only).

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
| `/api/policies/{id}/coverage` | GET | Coverage of one policy for the FNOL Step 1 display (Assessment §3.3.2, FRS §5.2). Returns `{ policyId, policyNumber, status, effectiveDate, expirationDate, coverageTypes: string[] }`. `404` if the policy doesn't exist. |

> **Resolved (requirements.md §9.5):** `GET /api/policies/{id}/coverage` (Assessment §3.3.2) is implemented as a read of the seeded `CoverageTypes` for the FNOL Step 1 "available coverage types" display (FRS §5.2). It performs no coverage evaluation, which FRS §2 keeps out of scope.

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
- Idempotency: write endpoints support an `Idempotency-Key` request header (FRS §10 intro) — see §5a
- All endpoints are RESTful JSON; kebab-case plural route nouns (FRS §15.3)
- No endpoint proxies document bytes — document retrieval always returns a SAS/local URL, never streams the file through the API (FRS BR-D-02)

## 5a. Idempotency-Key

Applies to `POST`, `PUT`, `PATCH`, `DELETE` when the `Idempotency-Key` header is sent (1–200 characters); requests without it are unaffected. Keys are scoped per organisation and kept for 24 hours.

| Situation | Response |
|---|---|
| New key | Request executes normally; a `2xx` response (status, body, content type, `Location`) is stored |
| Same key, same request (method + path + user + body hash) | Stored response replayed without re-executing, with header `Idempotency-Replayed: true` |
| Same key, different request | `422` — `errors["Idempotency-Key"]`: "This Idempotency-Key was already used for a different request." |
| Same key while the first request is still executing | `409` — "A request with this Idempotency-Key is still being processed." |
| First request failed (non-`2xx` or exception) | Nothing stored; the key can be retried |
| Key empty or longer than 200 characters | `422` |

An in-progress record older than 2 minutes is treated as abandoned and taken over. Expired records are removed hourly by the `idempotency-cleanup` Hangfire job.

## 6. Field-Level Validation Reference

See `docs/business-rules.md` §12 for the full field/rule/severity table that FluentValidation validators must implement for claim and reserve commands.
