# Legacy source delivery fixtures

Captured by executing the unmodified GoldMiner core, catalog, and save codec from commit `9074aac8b9930c3568d29151fcd3377562a66a31`. These are serialized runtime states, not manually assembled pending objects.

The old assembly was compiled in `/tmp/xanadu-gold-legacy-delivery` using `git show 9074aac:<path>`. Level 1 was created through `CreateSourceStart`, followed by `StartLevel`, `TryLaunch`, and `Step`. The fixture pair records consecutive real ticks immediately before and after the old return-to-top transition.

| Fixture | Seed | Return tick | Object | Old wait | Expected delivered result |
| --- | ---: | ---: | ---: | --- | --- |
| solo-money | 1 | 112 | 2 (medium gold) | 54 ticks | cash 100 |
| solo-power | 1 | 224 | 12 (power bag) | 54 ticks | existing cash 100, strength 5 |
| solo-dynamite | 4 | 76 | 1 (dynamite bag) | 30 ticks | cash 0, bombs 1 |
| coop-reset | 1 | 55 | 2 (small gold) | 6 ticks, already paid | cash 50 |

Solo commands target the first available bag (otherwise the first available object). Launch when the swinging hook angle is within `radius / distance * 0.45` radians of its ray; do not modify hook, entity, or wallet state. Duo sends launch commands for both players every 70 simulation ticks. IDs are monotonically increasing command sequence numbers.

New delivery tests use fixed known outcomes rather than recalculating expected rewards through current rule helpers. They cover the first arrival tick, legacy continuation through `GoldMinerSession`, normal dirty/save propagation, repeated encoded reloads, the final active tick, and preservation of already-draining historical balances.

SHA-256 of the captured files:

- `legacy-coop-reset-before-return.json`: `5fc110a3c441807eecad8d1b5f424728e29d5cfe2b7bc32711418dcef4453690`
- `legacy-coop-reset.json`: `86063e973805f23b8e5eb02e4cd734e500a2b95c37951b158356292e98a8c9a7`
- `legacy-solo-dynamite-before-return.json`: `41c2056987a3fa8c0faff48a244917d3040a6697fc3039c580dcaffb440665e4`
- `legacy-solo-dynamite.json`: `8498ad7abc5c0f0b24a3c6aa98c3875c2065069337799f6fb0cd0930a562657e`
- `legacy-solo-money-before-return.json`: `02eec688e16e3e6a50d9547d2d05f5d471b7946668370e80e99ff8d093b66bf5`
- `legacy-solo-money.json`: `1fd282222d2dd7656c5e8643c5a8cadfe715ce015009dd6376ba02af1562d484`
- `legacy-solo-power-before-return.json`: `f09719aaf4f595a61ea1da31294505211141d4a24429c099f76c790981c629bc`
- `legacy-solo-power.json`: `9631e583e8e9197d1fb94910ebb38fc0cc08fdc958121dfeef05e01564bf28f3`
