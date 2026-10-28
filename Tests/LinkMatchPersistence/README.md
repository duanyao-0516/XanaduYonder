# Link Match session and persistence checks

Run `scripts/test-link-match-persistence.sh` from the repository. The harness uses .NET 10, C# 7.3 and Newtonsoft.Json 13.0.2, compiling the production pure Core and five Unity-independent Runtime model/storage sources directly. It does not require a Unity editor or write user saves. File tests create and remove unique operating-system temporary directories.

The checks exercise:

- Readiness, visibility, pause and foreground clock gates; explicit foreground resume; exact challenge expiration and last-pair acceptance before feedback; relaxed play, successful-action quotas and free automatic deadlock rescue.
- Defensive restoration, persisted RNG state, six independent mode/level slots, preview without attempts, latest unfinished ordering, fresh-attempt replacement, stale-capture rejection, terminal accounting and best time/assistance from the same attempt.
- Strict required Newtonsoft token fields, types, nulls, unsupported identifiers, malformed JSON, duplicates, nonfinite/overflow numbers, quotas, clock and terminal/record consistency, board shape and even per-fruit counts. Valid deadlocked states are accepted for runtime recovery; a save is not required to carry a complete solver proof.
- Real flushed temporary files and atomic replacement; valid previous primary as backup; invalid UTF-8, oversized and corrupt main/backup input; corrupt-file archival; revalidation after external edits; failed writes and successful retry without duplicate records; unrelated game file preservation.

`LinkMatchSession.Tick` consumes a caller-supplied foreground frame delta. Construct/restore is inactive until `Enter`, backgrounding blocks, and foregrounding alone does not unblock. The page must settle time before pause/actions, and reset its monotonic frame baseline after a concealed synchronous automatic rescue. No wall-clock background duration is charged. `TryMatch` commits the outcome immediately; the page captures it before animation. `EnsureMove` can prepare a concealed/restored board and does not consume a manual shuffle. `TryHint` does not silently rescue.

The initial run failed because the five Runtime source files did not yet exist. After implementation, the first 25 behavioral groups passed. Additional behavior tests reproduced and then verified fixes for a missing null-record error reason, background capture overwriting preview selection, and a first/only winning attempt disagreeing with its best-record attempt ID (found in independent review). The completed suite contains 32 behavioral groups. These checks establish model and real local-filesystem behavior, not Unity rendering, Android build, physical-device input or device durability across power loss.
