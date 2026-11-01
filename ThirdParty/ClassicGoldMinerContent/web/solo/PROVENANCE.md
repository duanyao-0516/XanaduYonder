# Solo source and adaptation

Source archive: `artifacts/gold-miner-single-230816/original/`, archived from https://www.4399.com/flash/230816.htm.

Original `goldminer.min.js` SHA-256: `d4237da9e81c03cebe6f3cb2d599a2f0212b01a1b09680c60719f4a2734f904e`.

`phaser.js` is the unchanged bundled Phaser 2.4.6 engine prefix. `game.js` retains the original level layouts, sprites, collision/pickup rules, goal formula, prices, bonuses, hook speeds and game classes. The original CryptoJS/GA telemetry, unused WebFont loader, sponsor Spine/splash/links and sponsor atlas frame metadata are excluded. No missing audio alternatives or sponsor artwork files are included. Original atlas, bitmap font, four level backgrounds and ten OGG sound effects are local.

`adapter.js` owns the local boot, full-height mine viewport, direct touch controls, two-tap shop confirmation, versioned full-run snapshot and lifecycle. Gameplay uses logical height 480 and an aspect-derived width bounded to 640–1920. Source mineral X coordinates expand once; miners, minerals and claw sprites retain their proportions. Other scenes keep their 640 x 480 design at the largest uniform scale that fits. There is no permanent control panel: the mine reacts to the first press, and a 58 CSS-pixel floating bomb button sits above the safe bottom/right edges. Native safe-edge presses, HUD presses, and the bomb overlay cannot also drop the claw. `state.js` validates snapshot mode, schema, types and bounds. Shared host storage is the sole persistence path; no Unity or old H5 save key is read or changed.

Snapshots preserve selected map and remaining entities (including generated bag contents, claimed state and moving-tween direction/progress), hook/miner state, countdown tick remainder, delayed reward phase, goal/result transition remainder, scene fades, bonuses and original randomized shop inventory/prices/purchase state. Reward timing is explicit (money/strength 900 ms, dynamite 500 ms) so in-flight grants survive process exit without repetition. Merchant blink and background/gold shimmer are decorative and restart when reconstructing a scene.

The source shop allows debt: affordability is not checked by any upstream item handler. This rule is retained; the first tap shows price and cash, and the second tap buys once. Swipe-to-bomb has been replaced with a dedicated button; safe playfield press drops immediately.

Shop item and price-label hit areas expand according to the displayed canvas size, targeting 48 CSS pixels while staying inside separate item slots. The next-level touch area expands similarly. On extremely narrow canvases the available slot width limits the hit target; neighboring items never overlap. Artwork, prices and purchase handlers are unchanged.

Validation scripts are temporary under `artifacts/classic-gold-miner-validation/solo/`. Browser and Android validation is performed by the integration owner.

`layout.js` performs a pure, validated migration of full snapshots when the viewport changes. Schema 1 gameplay adds optional `viewportWidth` (missing means the original 640) and optional canonical swing angle. Source map definitions remain unchanged, world positions and moving-tween X endpoints migrate once, and sprite-local animation offsets retain their size. A separate canonical swing target retains the original two-second sine tween; its displayed/firing angle is projected to match the expanded mine so previously reachable side ores remain reachable. Resizing pauses input/time and synchronously rebuilds the current scene from the captured snapshot without rerolling map choice, bag contents or shop prices. Old in-flight Android snapshots are included in temporary regression fixtures.

Prices, weights, hook speed constants, level timers, swing period and original map contents are preserved. Expanding horizontal travel changes distances and therefore can change difficulty and timing of individual grabs; this layout is not claimed to reproduce original difficulty exactly. Moving-entity tween capture also fixes the earlier adaptation assigning a SignalBinding instead of the actual Tween.
