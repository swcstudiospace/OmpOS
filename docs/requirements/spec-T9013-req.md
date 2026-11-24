# Requirements specification T9013-req

Status: PROVISIONAL (inputs[] empty; brief truncated at docs/open-questi; assumptions in brief-T9013-req.md). Task T9013-req, correlation 901308c5-825d-45fe-8832-36b59c0771ae, capability req.spec, risk medium.

## 1. Goal
Wave 1 executes verification and inventory only as grounded in the brief: omp version facts are observed at 19.1.1 or verified at 18.12.3/18.12.6 and never invented; ACP is confirmed as production transport across Harness.Acp, EngineHost, Api, AgentWorker, Native and Harness.Remote; Rpc production consumers are counted; terminal ownership, provisioning, plugin inventory, roadmap append rule (79+), and blocked/paused/deferred phase state are carried forward exactly; Waves 2+ return plans only. Baseline verified by read: branch gsd/v0160-wave4-computer at 983c4594, existing requirement ids end at US-209 / NFR-C2, so this spec starts at US-301 and NFR-P3.

## 2. Stories and criteria
Stories US-301 to US-310 in stories-T9013.md; criteria in acceptance-T9013.md (1 criterion per story, all automated); NFRs NFR-P3, NFR-S3, NFR-A3, NFR-I3, NFR-C3 in nfr-T9013.md; links in traceability-T9013.md.

## 3. Priority (MoSCoW, 10 stories)
Must: US-301, US-302, US-303, US-307, US-308, US-309. Should: US-304, US-305, US-306. Could: US-310 (applies only on tracker failure). Won't (this wave): terminal owner retirement, Rpc removal, Herdr removal, any Waves 2+ execution.

## 4. Out of scope
Architecture, product code, schema, release actions, tracker writes by this task, archive of v0.16.0, clearing SEC-77-01, 68-09 SEC-77-07, 74.1 review, 75-12 hold, or deferred-human phases.

## 5. Assumptions and open questions
A-1 Wave 1 first execution slice is sequenced by A01 (OQ-2). A-2 Truncated brief tail adds no Wave 1 product change (OQ-1). A-3 Omp 19.1.1 deltas are recorded only from observation (OQ-3). Confidence: medium; PROVISIONAL until OQ-1 and OQ-2 close.
