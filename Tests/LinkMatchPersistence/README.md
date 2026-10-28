# Link Match session and persistence checks

Run `scripts/test-link-match-persistence.sh` from the repository. The harness uses .NET 10, C# 7.3 and Newtonsoft.Json 13.0.2, compiling the production pure Core and five Unity-independent Runtime model/storage sources directly. It does not require a Unity editor or write user saves. File tests create and remove unique operating-system temporary directories.

The checks exercise:

- Readiness, visibility, pause and foreground clock gates; explicit foreground resume; exact challenge expiration and last-pair acceptance before feedback; relaxed play, successful-action quotas and free automatic deadlock rescue.
- Defensive restoration, persisted RNG state, ten independent mode/level slots (including regression coverage for the original six), preview without attempts, latest unfinished ordering, fresh-attempt replacement, stale-capture rejection, terminal accounting and best time/assistance from the same attempt.
- Strict required Newtonsoft token fields, types, nulls, unsupported identifiers, malformed JSON, duplicates, nonfinite/overflow numbers, quotas, clock and terminal/record consistency, board shape and even per-fruit counts. Valid deadlocked states are accepted for runtime recovery; a save is not required to carry a complete solver proof.
- Real flushed temporary files and atomic replacement; valid previous primary as backup; invalid UTF-8, oversized and corrupt main/backup input; corrupt-file archival; revalidation after external edits; failed writes and successful retry without duplicate records; unrelated game file preservation.

`LinkMatchSession.Tick` consumes a caller-supplied foreground frame delta. Construct/restore is inactive until `Enter`, backgrounding blocks, and foregrounding alone does not unblock. The page must settle time before pause/actions, and reset its monotonic frame baseline after a concealed synchronous automatic rescue. No wall-clock background duration is charged. `TryMatch` commits the outcome immediately; the page captures it before animation. `EnsureMove` can prepare a concealed/restored board and does not consume a manual shuffle. `TryHint` does not silently rescue.

The initial run failed because the five Runtime source files did not yet exist. After implementation, the first 25 behavioral groups passed. Additional behavior tests reproduced and then verified fixes for a missing null-record error reason, background capture overwriting preview selection, and a first/only winning attempt disagreeing with its best-record attempt ID (found in independent review). The original 32 behavioral groups remain in the suite. The expansion adds 13 groups, for 45 total. These checks establish model and real local-filesystem behavior, not Unity rendering, Android build, physical-device input or device durability across power loss.


## v1 to v2 expansion and fixtures

The current writer/decoder uses schema/content version 2 and `link-match-v2.json`. The separate `TryDecodeLegacyV1` boundary requires schema/content 1, the original six slots/records, original level IDs 1–3 and each original mode/level index. Legacy boards are limited to 48 cells. After validating those old identities it maps by mode and stable level ID: relaxed 0/1/2 stay 0/1/2, challenge 3/4/5 become 5/6/7. New slots 3/4/8/9 stay empty; the full current validation then runs over the migrated profile. Current saves accept up to 192 cells from the five-level catalog.

The store consults current primary and backup first. It only reads legacy files if both current files are missing; a corrupt or unreadable current file prevents rollback to legacy progress. A successful migration flushes and atomically commits v2 while retaining both v1 originals. If that write fails, `Load` still returns every migrated field and sets `MigrationPending` plus a warning. The page initializes its save-error state from that flag. A later successful normal `TrySave` clears the flag without recounting terminal results.

The immutable `Fixtures/legacy-v1-primary.json` and `legacy-v1-backup.json` were produced **before migration edits** by the unmodified Core and five Runtime production sources from Git commit `e59247cfd6f55895c4cd770153c681cdd95afd72`. `Fixtures/GenerateLegacyV1.cs` records the producer; it was compiled with those extracted sources in a temporary project. Each result was round-tripped by that original codec before being saved. The producer uses actual generated games and sessions, including all three unfinished challenge slots and prior winning records. Do not regenerate these fixtures during tests; their original bytes are checked with these SHA-256 values:

- Primary: `b6f06178aba7e6fbbab04789cdbe694913830cfb9922a68d093ea92102d68dba`
- Backup: `ec682dc1aaa3751c5de6cdf5d742ca3efe4e8dcf0578549d7514956486fc9e45`

Expansion red/green evidence: before the ten-slot implementation the new behavioral test failed with `6 != 10`; after the current format was implemented the real-file migration test failed with `Valid production v1 progress was not migrated`; both pass with the final implementation. Tests compare **all** original slot and record JSON fields with their mapped current identities, cover required fields/null/wrong types/wrong old indices, legacy backup recovery, corrupt-v2 no-rollback, failed atomic migration and retry, ten-slot roundtrip, and final records for 192-cell play.

For an explicitly authorized local legacy file, `scripts/test-link-match-persistence.sh --verify-legacy /absolute/path/to/save.json` runs the same strict migration decoder and full old-field comparison read-only. It reports counts only and does not copy the input, print identities, or write a migrated file. Private device files must remain outside tracked fixtures and documentation.
