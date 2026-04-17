---
name: milestone-workflow
description: "End-to-end workflow for delivering a multi-issue milestone from planning to merged, documented code. Use when executing a milestone (phase) containing multiple related issues that ship as a single PR."
---

# Milestone Development Workflow

## Overview

End-to-end workflow for delivering an entire milestone (phase) from architect review to
merged code. Use this workflow when executing a group of related issues that belong to the
same milestone and ship together as a single PR. This is the standard workflow for phased
delivery — it extends the feature-workflow with wave-based implementation, parallel review
pipelines, and milestone lifecycle management.

For single-feature work that doesn't belong to a multi-issue milestone, use the
`/feature-workflow` instead.

## Trigger

A human requests work on a specific milestone (e.g., "Begin on Phase 3"). The milestone
must already exist in GitHub with issues assigned to it.

## Steps

| # | Role | Action | Inputs | Outputs | Success Criteria |
|---|------|--------|--------|---------|------------------|
| 0 | **Orchestrator** | Initialize workflow: create state file, validate milestone exists, list issues | Milestone name/number | `.teamwork/state/<id>.yaml`, issue inventory | State file created with status `active`; all issues listed |
| 1 | **Architect** | Review all milestone issues for completeness, feasibility, dependencies; produce wave plan | Issue list, existing codebase | Feasibility assessment per issue, dependency graph, wave plan, ADR (if needed) | Every issue has sufficient acceptance criteria; waves form a valid DAG |
| 2 | **Orchestrator** | Create branch `milestone/<phase-name>`, open Draft PR, link all issues | Wave plan, milestone metadata | Branch, Draft PR with issue checklist | PR created and linked to milestone |
| 3 | **Coder** | Implement issues in wave order; link each issue to PR when work begins; commit per wave | Wave plan, design decisions, conventions | Commits on milestone branch, issues linked to PR | Each wave's code compiles and tests pass locally before next wave |
| 4 | **Tester** | Review test coverage, write edge-case tests, validate against acceptance criteria per wave | PR branch, acceptance criteria | Additional tests, coverage assessment, defect reports | Acceptance criteria verified; edge cases covered; coverage meets thresholds |
| 5 | **Orchestrator** | Run local test suite, verify all issues implemented, mark PR ready for review | PR with all waves complete | PR marked "Ready for Review", test results | All tests pass locally (`dotnet test` + `npx tsc --noEmit`) |
| 6 | **Review Pipeline** | Run code-review + dba-agent + security-auditor agents **in parallel** on the PR | PR diff | Three review reports with findings (severity, location, remediation) | All three reviews complete |
| 7 | **Architect** | Triage all findings into Fix Now / Follow-up / Tech Debt categories | Three review reports | Triage table with categorized findings and batched fix instructions | Every finding categorized; Fix Now items have clear instructions |
| 8 | **Coder** | Fix all "Fix Now" items **sequentially** (one coder agent per batch) | Triage table, fix instructions | Commits addressing findings | All Fix Now items resolved; tests pass |
| 9 | **Review Pipeline (Round 2)** | Re-run code-review + dba-agent + security-auditor on the fixes | Updated PR diff | Second review reports | No new high/critical findings introduced by fixes |
| 10 | **Architect** | Triage Round 2 findings; if new Fix Now items exist, loop to step 8 (max 3 rounds) | Round 2 review reports | Updated triage table or "all clear" | No unresolved Fix Now items |
| 11 | **Orchestrator** | Verify all CI checks green, all review findings addressed | CI status, triage results | CI verification report | All CI checks pass; no unresolved high/critical findings |
| 12 | **Human** | Approves and merges the PR | Approved PR, CI green, review summary | Merged code on main branch | Code merged; CI passes on main |
| 13 | **Documenter** | Update MEMORY.md with lessons learned, update CHANGELOG, close milestone issues | Merged PR, implementation notes | Updated docs, closed issues, closed milestone | All issues closed; milestone closed; MEMORY.md updated |
| 14 | **Orchestrator** | Complete workflow: update state file, log metrics, create follow-up issues for deferred items | All step outputs, deferred items list | State file `completed`, follow-up issues created | Workflow finalized; no loose ends |

## Handoff Contracts

Each step must produce specific artifacts before the next step can begin.
Handoffs are stored in `.teamwork/handoffs/<workflow-id>/`.

**Human → Orchestrator (Step 0)**
- Milestone name or number
- Any special instructions or constraints

**Orchestrator → Architect (Step 1)**
- Issue inventory with titles, bodies, and current labels
- Existing codebase context relevant to the milestone

**Architect → Orchestrator (Step 2)**
- Feasibility assessment per issue (comments on issues if needed)
- Wave plan: ordered groups of issues with dependencies
- Design decisions and conventions to follow
- ADR file (if the milestone introduces new patterns)

**Orchestrator → Coder (Step 3)**
- Branch name and PR number
- Wave plan with implementation order
- Design decisions from architect
- Instruction: link each issue to PR when work begins

**Coder → Tester (Step 4)**
- PR branch with wave implementation and initial tests
- List of acceptance criteria per issue

**Tester → Orchestrator (Step 5)**
- PR branch with complete test suite
- Coverage assessment
- Any defect reports (loop back to Coder if critical)

**Orchestrator → Review Pipeline (Step 6)**
- PR number for review
- List of changed files

**Review Pipeline → Architect (Step 7)**
- Three review reports (code-review, dba-agent, security-auditor)
- Findings with severity, location, and suggested remediation

**Architect → Coder (Step 8)**
- Triage table: Fix Now items grouped into sequential batches
- Clear instructions per batch: files to modify, what to change, test expectations

**Review Pipeline → Architect (Step 9)**
- Round 2 review reports
- Delta from Round 1 (new findings only)

**Orchestrator → Human (Step 12)**
- PR link, CI status, review summary
- List of deferred items (Follow-up / Tech Debt)

**Human → Documenter (Step 13)**
- Merged commit on main

**Documenter → Orchestrator (Step 14)**
- Updated MEMORY.md, CHANGELOG
- Closed issues and milestone
- List of follow-up issues created

## Wave-Based Implementation

Issues within a milestone are grouped into **waves** based on dependencies:

```
Wave 1: Foundation (no dependencies)
  ├── Issue A (infrastructure/models)
  └── Issue B (independent utility)

Wave 2: Core components (depends on Wave 1)
  ├── Issue C (depends on A)
  └── Issue D (depends on A)

Wave 3: Integration (depends on Wave 2)
  ├── Issue E (depends on C + D)
  └── Issue F (depends on D)

Wave 4: Orchestration + Testing (depends on all above)
  ├── Issue G (orchestrator - depends on C, D, E, F)
  └── Issue H (integration tests - depends on G)
```

### Wave Rules

1. **All issues in a wave can be implemented in parallel** (if using separate files), but
   **coder agents must be dispatched sequentially** to avoid git conflicts in the shared
   working directory.
2. **Each wave produces a commit** (or small group of commits) that compiles and passes tests.
3. **Link each issue to the PR** when work begins on that issue, not when the wave completes.
4. **Run `dotnet test` after each wave** to catch regressions early.
5. **Never start a wave until the previous wave's tests pass.**

## Review Pipeline

The review pipeline replaces GitHub Copilot PR reviews with internal specialized agents:

```
┌─────────────────────────────────────────────────────┐
│  Step 6: Launch three agents IN PARALLEL             │
│                                                      │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ │
│  │ code-review  │ │  dba-agent   │ │  security-   │ │
│  │    agent     │ │              │ │   auditor    │ │
│  └──────┬───────┘ └──────┬───────┘ └──────┬───────┘ │
│         │                │                │          │
│         └────────┬───────┴────────┬───────┘          │
│                  ▼                                    │
│  Step 7: Architect triages all findings               │
│          → Fix Now / Follow-up / Tech Debt            │
│                  │                                    │
│                  ▼                                    │
│  Step 8: Coder agents fix (SEQUENTIALLY)              │
│                  │                                    │
│                  ▼                                    │
│  Step 9: Round 2 review (same 3 agents)               │
│                  │                                    │
│                  ▼                                    │
│  Step 10: Architect validates (loop if needed, max 3) │
└─────────────────────────────────────────────────────┘
```

### Review Pipeline Rules

1. **Always run all three agents** — even if the milestone has no database changes (the DBA
   agent may catch issues in SQL string construction or connection handling).
2. **Architect triages findings** — coders do not self-triage. The architect determines what
   must be fixed now vs. deferred.
3. **Coder agents are dispatched sequentially** — never in parallel. Each agent gets explicit
   file-level instructions and must not create new branches or push.
4. **Budget for 2 review rounds** — fixes can introduce new issues. A third round is the
   maximum before escalating to the human.
5. **No unresolved high/critical findings** may remain when the PR is marked ready for merge.

## Milestone Lifecycle

```
┌───────────────┐
│ Milestone     │──► Create branch `milestone/<name>`
│ exists in GH  │──► Open Draft PR linking all issues
└───────┬───────┘
        │
        ▼
┌───────────────┐
│ Implementation│──► Waves 1..N with issue linking
│ (Steps 1-5)   │──► Tests pass after each wave
└───────┬───────┘
        │
        ▼
┌───────────────┐
│ Review +      │──► Parallel review pipeline
│ Hardening     │──► Architect triage + coder fixes
│ (Steps 6-11)  │──► Up to 3 review rounds
└───────┬───────┘
        │
        ▼
┌───────────────┐
│ Merge +       │──► Human approves merge
│ Closeout      │──► Close all issues + milestone
│ (Steps 12-14) │──► Update MEMORY.md + CHANGELOG
                │──► Create follow-up issues for deferred items
└───────────────┘
```

## Completion Criteria

- All issues in the milestone are implemented, tested, reviewed, and merged.
- No unresolved security findings at high or critical severity.
- All CI checks pass on the merged code.
- MEMORY.md updated with lessons learned and architecture additions.
- CHANGELOG updated with milestone summary.
- Milestone closed in GitHub.
- Follow-up issues created for deferred findings (Follow-up and Tech Debt categories).
- Workflow state file updated to `completed`.

## Notes

- **Review loops**: If Round 2 introduces new Fix Now items, loop back to Step 8. Maximum
  3 rounds total before escalating to the human. This prevents infinite review cycles.
- **Sequential coder dispatch**: Parallel coder agents sharing one working directory cause
  git conflicts. Always dispatch sequentially, verify each agent's work, then dispatch next.
- **Issue linking convention**: Link each issue to the PR as soon as work begins on that
  issue — don't batch link at the end. This provides real-time traceability.
- **Scope control**: If the architect identifies scope creep during review, deferred items
  go to follow-up issues — never expand the PR scope mid-review.
- **Tester role**: The tester reviews coverage and writes edge-case tests AFTER the coder
  finishes each wave. This catches gaps the coder missed. The tester does NOT block wave
  progression — they work on the previous wave's tests while the coder starts the next wave.
- **Orchestrator state**: Use `.teamwork/state/` files for workflow tracking. The state file
  is the source of truth for where the workflow is and what has been completed.
- **Relationship to feature-workflow**: This workflow is a superset of the feature-workflow.
  For single-issue work, use `/feature-workflow`. For multi-issue milestones, use this.
- **CI as gate**: The PR cannot be merged until all CI checks pass. If CI fails after fixes,
  debug and fix before requesting human approval.
