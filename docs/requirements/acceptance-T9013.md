# Acceptance criteria T9013-req

AC-301 story: US-301 Given omp 19.1.1 installed and codebase verified at 18.12.6 When the Wave 1 inventory records a version fact Then the record labels it observed or verified with 0 unlabeled facts check: source-grep automated: true

AC-302 story: US-302 Given the 7 named production component groups in the brief When each group is checked for its transport Then 7 of 7 groups record ACP as transport check: source-grep automated: true

AC-303 story: US-303 Given production projects excluding test projects When references to the Rpc client, session and process types are counted Then the production consumer count equals 0 check: source-grep automated: true

AC-304 story: US-304 Given terminal sessions in the native session engine set When Wave 1 changes are diffed Then the count of changed session owner assignments equals 0 check: diff-check automated: true

AC-305 story: US-305 Given the managed installer and locator in the launcher project When the dead component catalogue is written Then both provisioning types remain present with count 2 check: source-grep automated: true

AC-306 story: US-306 Given plugin contribution points named in the brief When the inventory lists each point and its pinned tree Then 100 percent of listed points name 1 source tree and 0 list the uninitialized realm tree as active check: source-grep automated: true

AC-307 story: US-307 Given the cumulative roadmap ending at phase 78 When a new milestone phase is appended Then the first appended phase number is 79 and 0 v0.16.0 phases are archived check: doc-check automated: true

AC-308 story: US-308 Given deferred phases 66.1, 66.2, 66.3, 66.4 and 69, paused phase 68 and blocker SEC-77-01 When Wave 1 status is recorded Then all 7 identifiers appear unchanged and 0 are marked complete check: doc-check automated: true

AC-309 story: US-309 Given Waves 2 onward in the programme When the Wave 1 result is returned Then Waves 2 onward contain plans with 0 executed product changes check: diff-check automated: true

AC-310 story: US-310 Given a tracker write that returns an error When the result records the failure Then it stores the exact error text plus 1 payload file and fabricates 0 tracker identifiers check: doc-check automated: true
