# AI Workflow Report

> **Draft for review.** Written from the project history; edit it into your own words and complete the items marked ✏️ before submitting.

## 1. Tools

| Tool | Used for |
|---|---|
| **Claude Code** (Claude, CLI on Windows) | requirements extraction, architecture reasoning, implementation of backend, frontend, IaC and pipelines, tests, code review against the FRS, documentation drafts |
| ✏️ *(add any other tool you used, e.g. Copilot in the IDE, ChatGPT)* | |

## 2. How the workflow was structured

**Context first, code second.** The FRS and the assessment brief (DOCX) were extracted to text and loaded as context. Before any code, Claude produced a structured requirements set in `docs/` — `requirements.md`, `business-rules.md`, `domain-model.md`, `api-contract.md`, `architecture.md` — followed by a review pass looking for gaps, contradictions and invented requirements between the two documents. Conflicts were decided by me and recorded as ADRs in `docs/decisions.md` (state machine, `PUT` for reserve adjustment, $10M override storage, no per-role reserve cap). From then on, the standing instruction was *"always follow the instructions from docx files"*, and every feature was checked against those documents.

**Plan → approval → implement → verify, for every change.**
- For every non-trivial change Claude first showed the exact planned edits (files, code, tests, commands); I approved, changed or rejected them before anything was written.
- **Permission gates:** I configured Claude Code so that every terminal command (dotnet, npm, git, Azure CLI, Docker) needs my approval, and never allowed Azure or GitHub changes without an explicit request.
- After each change: build, full test suite, and a short summary of what changed and what was *not* verified.

**Vertical slices and phases.**
- **Backend:** domain model and EF configuration → claim creation → status transitions → parties → reserves and the GL posting job → SLA job → documents → CI.
- **Infrastructure:** Bicep templates with managed identity (no secrets) → OIDC-based CD pipelines.
- **Frontend:** five phases (shell → dashboard → FNOL → claim detail → tests and polish), with a review after each.
- **Gap analyses:** repeatedly asking *"what is left regarding the docx requirements?"* surfaced missing items: domain events, tenant filter, `Idempotency-Key`, the coverage endpoint, SQL seed scripts, BR-C-02 audit recording and the GL retry.

**Persistent preferences.** Code-style and process rules I gave once were saved as memory and applied afterwards:
- records on one line
- no FRS/rule-ID rationale comments in production code
- no subscription details in docs
- propose production-code changes before making them

## 3. Representative prompts

1. **Scope and source of truth.**
   > "lets proceed with frontend (always follow the instructions from docx files)"

   *Purpose:* anchor all generated UI to FRS §11 and assessment §3.7 rather than to generic patterns or the visual mock-ups. The resulting plan explicitly separated "look from the design examples" from "content from the FRS" and left out invented features.

2. **Constraining a design decision.**
   > "Please keep Claim.Id database-generated with SQL Server NEWSEQUENTIALID(), as required by the specification. Do not switch Claim.Id to application-generated SequentialGuid or use ValueGeneratedNever(). For initial reserve creation, use an explicit database transaction: 1. Insert Claim and SaveChanges() to obtain the database-generated Claim.Id. 2. Create the initial reserve/history using the real Claim.Id. 3. SaveChanges() again. 4. Commit the transaction. If reserve creation fails, roll back the whole transaction so claim creation remains atomic."

   *Purpose:* replace an AI-proposed shortcut with the design the specification requires (see correction 1).

3. **Precise, verifiable operational change.**
   > "Proceed with the npm lockfile fix. Also pin the exact Node version in GitHub Actions to match local development instead of using a floating 24.x. Update packageManager accordingly, regenerate package-lock.json with the chosen npm version, run npm ci, build and tests, then show me the changed files before committing."

   *Purpose:* fix a CI failure reproducibly, with an explicit verification and review step.

4. **Putting the AI under explicit control.**
   > "Always ask for permission before running any shell, Bash, PowerShell, Azure CLI, Git, Docker, dotnet, npm, or other terminal command."
   >
   > "Before editing files, show me the final exact file changes you plan to make."

   *Purpose:* after the AI ran an Azure deployment I had not asked for, I turned the workflow from "AI acts, I review afterwards" into "AI proposes, I approve". From then on every change arrived as an exact plan I could accept, change or reject, and no command touched my machine, the repository or Azure without my OK. This is the decision that made the rest of the project safe to delegate: it caught several unwanted changes before they happened (a production-code change for a local Docker tool, database commands against my local database, extra scope in plans), and it gave me the review points this report is built on.

5. **Requirement coverage check.**
   > "is everything for BE done regarding docx requirements?"

   *Purpose:* force a requirement-by-requirement comparison against the assessment. It found that domain events existed in the base class but were never raised, even though the brief lists "ClaimCreated triggers audit log entry" as a review point.

## 4. What was AI-generated and what I designed or decided

| Area | AI-generated | Designed / decided / refined by me |
|---|---|---|
| Requirements | extraction and cross-checking of FRS and brief | resolution of every conflict (ADRs), what is in or out of scope |
| Backend | most code: entities, EF configurations, handlers, validators, jobs, middleware, tests | Clean Architecture boundaries and conventions; keeping reserves inside the `Claim` aggregate (no separate reserve repository); the `Claim.Id` / transaction design; naming (e.g. `MarkParentClaimsAsUpdated`); readability refactors of query handlers; code style rules |
| Security & Azure | Bicep templates, workflow YAML | managed identity everywhere, Entra-only SQL, no storage keys, **GitHub OIDC only** (no client secrets or publish profiles), explicit resource-name variables instead of lookups, one-time manual SQL grant kept out of CD, regions and SKUs |
| Frontend | components, forms, services, styles, tests | Angular version choice (21 LTS for the local Node), signals + services for state, following the design examples visually but the FRS for content, which review findings to fix |
| Local setup | Dockerfiles and compose | "Docker must not require code changes", no storage connection strings in the repo |
| Deviations | analysis of options | which deviations to accept and document (see `ARCHITECTURE.md` §10) |

✏️ *Adjust this table to reflect what you personally wrote or rewrote.*

## 5. Where the AI was wrong and how it was corrected

1. **Database identity strategy.** To create an initial reserve together with a claim, Claude proposed making `Claim.Id` application-generated (`SequentialGuid`, `ValueGeneratedNever`). That contradicts the FRS convention (`NEWSEQUENTIALID()` keys). I rejected it and specified the alternative: an explicit transaction — save the claim to get its database id, record the reserve, save again, commit or roll back. The final implementation also enqueues the GL job only after commit, and has tests for the rollback path.

2. **A migration that would have deleted production data.** When reference data moved from EF `HasData` to SQL seed scripts, the generated EF migration contained `DeleteData` for all 10 cause-of-loss codes and 5 policies. Deployed, it would have wiped them in Azure. The migration was reduced to an empty one that only updates the model snapshot. Before that, I had also caught that the `HasData` seed was still left in the configuration.

3. **Unrequested infrastructure changes.** Claude ran an Azure deployment after an ambiguous instruction. I stopped it, and from then on required explicit permission for every Azure, GitHub, Git, package or file-modifying command. Every later command went through that gate.

4. **Scope creep in the Docker setup.** To make download links work with the Azurite emulator, Claude changed the production blob-storage service and committed the emulator's connection string to `docker-compose.yml` and a test. I pushed back: local tooling must not change application code or store storage connection strings. The final setup uses the FRS's own local-filesystem fallback (BR-D-03) with **no code changes** and no keys.

5. **CI pipeline bugs in generated YAML.**
   - The API smoke test ran under `bash -e` and wasn't retried, so one transient `502` during the App Service restart failed the deployment. It was fixed by making both checks part of the retried condition.
   - The frontend CI used a floating `24.x` Node, whose newer npm rejected the lock file. It was fixed by pinning the exact Node version used locally.

6. **Smaller generated-code defects found by tests or review:**
   - nested NSubstitute `Returns` calls in new tests
   - a test policy without effective dates, exposed by the new BR-C-02 rule
   - Angular templates using getters and method calls instead of signals
   - a PowerShell edit that re-encoded a template and garbled characters (detected and fixed)

## 6. Honest assessment

**Where AI helped most:**
- **Volume and consistency:** EF configurations applying the FRS conventions everywhere, dozens of command/query/validator/test sets following one pattern, Bicep and workflow YAML.
- **Cross-checking requirements:** comparing the implementation against two long documents, listing gaps and conflicts I would likely have missed (domain events, BR-C-02 audit recording, `Idempotency-Key`, the coverage endpoint).
- **Tests:** broad unit-test coverage of the business rules and edge cases, written alongside the code.
- **Frontend scaffolding:** multi-step reactive forms, Material theming and the typed API layer.

**Where it needed the most supervision:**
- **Staying in scope:** it sometimes changed more than asked (production code for a local tool, unrequested deployments). Approval gates and explicit constraints fixed this.
- **Choosing the simplest correct design:** it tended toward shortcuts or extra machinery. Asking it to justify each part, or to follow the specification literally, gave better results.
- **Environment specifics:** Windows file locks, line endings and encodings, npm/Node version drift and Azure regional restrictions all needed human diagnosis or confirmation.

✏️ *Add your own conclusion: what you would do differently, and how the workflow compares to working without AI.*

## 7. AI interaction history (§4.6)

✏️ Exported Claude Code conversation: *(add the export or share link here)*
