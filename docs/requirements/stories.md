# User stories T77d9-req

Status: Record (user stories for T77d9-req)

Each story carries stable identifier, role, capability, dependencies,
estimate and MoSCoW class. Estimates are points 1 to 8. The set has 22
stories, so ordering is by WSJF value computed from stakeholder scores.

## US-101 — Phase 66 rebrand guard

As a maintainer, I want the Autonogrammer rename guarded on the current
main line so that a later change never revives the former name.

Depends on: US-119
Estimate: 5
MoSCoW: Must

## US-102 — Phase 66.1 scaffold

As a maintainer, I want the shared project scaffold recorded as the single
starting shape so that every later phase builds on one structure.

Depends on: US-101
Estimate: 3
MoSCoW: Must

## US-103 — Phase 66.2 hosted web app

As a signed-in user, I want the hosted web app served as the real app
so that I always use the production surface rather than a demo.

Depends on: US-102
Estimate: 5
MoSCoW: Must

## US-104 — Phase 66.3 web accessibility

As a keyboard and screen-reader user, I want the web app to meet WCAG 2.2 AA
so that I can complete every flow without assistance.

Depends on: US-103
Estimate: 5
MoSCoW: Must

## US-105 — Phase 66.4 quality uplift

As a maintainer, I want operations, supply chain and brand checks recorded
on the current line so that each release ships with known provenance.

Depends on: US-104
Estimate: 5
MoSCoW: Must

## US-106 — Phase 67 accounts

As a returning user, I want passwordless sign-in by code and by link
so that I regain access without managing a password.

Depends on: US-102, US-105
Estimate: 8
MoSCoW: Must

## US-107 — Phase 68 data plane

As a maintainer, I want the data plane renamed, migrated and bounded
so that accounts, Cowork and Dispatch share one governed store.

Depends on: US-102
Estimate: 8
MoSCoW: Must

## US-108 — Phase 69 surface switch and Cowork sessions

As a user, I want a Code and Cowork switch that never mixes their sessions
so that I keep each surface separate while I work.

Depends on: US-106, US-107
Estimate: 5
MoSCoW: Must

## US-109 — Phase 70 Cowork chat core and tool host

As a Cowork user, I want chat with governed tool calls in that surface
so that I complete work without leaving Cowork.

Depends on: US-108
Estimate: 8
MoSCoW: Must

## US-110 — Phase 71 Cowork artifacts

As a Cowork user, I want versioned artifacts with safe rendering
so that I can review, edit and export work without script execution.

Depends on: US-109
Estimate: 8
MoSCoW: Must

## US-111 — Phase 72 Cowork browser use

As a Cowork user, I want browser use behind fail-closed audit and grants
so that I can browse within explicit permission only.

Depends on: US-109
Estimate: 8
MoSCoW: Should

## US-112 — Phase 73 Cowork computer use

As a Cowork user, I want computer use on the local device with explicit
approval so that I control what the agent can see and touch.

Depends on: US-109
Estimate: 8
MoSCoW: Should

## US-113 — Phase 74 Dispatch relay

As a user on any surface, I want Dispatch commands relayed to the paired
agent so that I can act away from the main device within agreed limits.

Depends on: US-109, US-112
Estimate: 8
MoSCoW: Must

## US-114 — Phase 74.1 memory idempotency

As a user with several devices, I want memory writes applied once each
so that I never see duplicated memories after a retry.

Depends on: US-107, US-113
Estimate: 5
MoSCoW: Must

## US-115 — Phase 75 Cowork parity A

As a Cowork user, I want projects, memory and schedules at parity
so that I organize ongoing work without repeated setup.

Depends on: US-109
Estimate: 8
MoSCoW: Should

## US-116 — Phase 76 Cowork parity B

As a Cowork user, I want plugins, Auto mode and isolation at parity
so that I extend Cowork while staying inside the isolation boundary.

Depends on: US-115
Estimate: 8
MoSCoW: Should

## US-117 — Phase 77 hardening, docs and integration

As a maintainer and a new reader, I want the last hardening and docs closed
so that the 0.19.x to 0.20.x line ships documented and gated.

Depends on: US-110, US-116
Estimate: 5
MoSCoW: Must

## US-118 — Phase 78 desktop release pipeline

As a desktop user, I want versioned installers with verified checksums
so that I install exactly the bits the release published.

Depends on: US-117
Estimate: 8
MoSCoW: Must

## US-119 — Preserve dirty files on a branch from origin main

As a maintainer, I want the 10 modified auth and secrets files preserved
on a branch from origin main with main left clean so that no work is
lost and no direct push to main occurs.

Depends on: US-101
Estimate: 3
MoSCoW: Must

## US-120 — Harden top State-of-the-Art gaps only

As a maintainer, I want the top quality gaps from phases 25.1 to 25.9
closed without new conventions and without shrinking scope
so that the codebase meets the current bar and stays maintainable.

Depends on: US-101
Estimate: 5
MoSCoW: Must

## US-121 — Pull request to main with all-green CI

As a maintainer, I want one pull request to main merged only when every
CI job is green so that main always holds verified work.

Depends on: US-118, US-119, US-120
Estimate: 3
MoSCoW: Must

## US-122 — STATE and ROADMAP update at the end

As a maintainer, I want STATE.md and ROADMAP.md updated at the end
so that the next run starts from the recorded position.

Depends on: US-121
Estimate: 2
MoSCoW: Must
