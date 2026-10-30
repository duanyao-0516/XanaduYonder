# Age of War 2 native simulation source map

## Inputs and ownership

The input is the complete offline 7k7k release, SHA-256 `b7dc9f9654add56e87234381e5272695592e3837bbd8489f836ed7225b81960c`. Its decompiled scripts and embedded-SWF metadata live under `/Users/bytedance/Downloads/age-of-war-research-20261030/analysis/aow2`. This is a source translation, not a replacement balance model. The `Two` core has no Unity, networking, filesystem, Flash-player or persistence dependency.

`AgeOfWarTwoBattle(double difficulty, uint seed)` accepts the original difficulty values `.6`, `1`, `1.5`, `2`. Whether the original release exposes Insane on a given origin is a menu responsibility; its health factor remains representable in the core. Original DATA and AI XML are embedded verbatim in `AgeOfWarTwoSourceData.cs`. Both contain the complete seven-age catalogs: 29 units, 17 turrets, 22 projectiles, 18 upgrades / 58 level elements, four tower levels and seven specials.

## Mapping

Source paths below are relative to `outer/scripts/net/louissi/`.

| Original | Native implementation |
| --- | --- |
| `Alpha1/Screens/GameScreen.as` initialization, render, evolve, damageBase | `AgeOfWarTwoBattle.cs` constructor, Tick, Evolve, DamageBase |
| `Alpha1/Hud/Hud.as` queueUnit, construction, buyUpgrade, addTower, buySpecial, buyEvolve | Execute, HUD phase of Tick, Capture |
| Hud buyTurret / build-slot / cancel | BuyTurret escrow, PlaceTurret, Cancel; PendingTurretId snapshot |
| `Alpha1/Computer/AI.as` | LoadAiAge, NextAiEvent, RenderAi; complete embedded AI event XML |
| `Alpha1/Managers/DamageManager.as` | Bonus and Damage |
| `Alpha1/units/Unit.as`, UnitsManager | `AgeOfWarTwoUnits.cs`: Unit and manager phase |
| `Alpha1/Turrets/Turret.as`, TurretsManager | Turret, aiming, source bp/bp2 transforms, retained animation registration on sale |
| `Moon/Animations/Animation.as`, AnimationData, MovieClipAnimation, MovieClipUtil.getAnimationData | `AgeOfWarTwoAnimation.cs`; exact label ranges and per-frame launch-point data in `AgeOfWarTwoTimelines.cs` |
| `Alpha1/Particules/Projectile.as`, Projectiles | `AgeOfWarTwoProjectiles.cs`: Projectile and manager phase |
| Dyn_Special1–7, Dyn_Meteor/Lightning/FireBall/Arrow/CanonBall/PlaneBomb/LaserBeam | Effect emitters and seven distinct strike paths |
| Dyn_Explosion, Dyn_FireTrail | Cosmetic effects with original animation clocks and sounds |
| `Alpha1/Windows/*Info.as` | `AgeOfWarTwoHud.cs` in Core: CaptureTooltip, CaptureHudText |
| Hud status overlays / queue / progress / cooldown / errors | `Runtime/AgeOfWar/AgeOfWarTwoHud.cs`; original extracted artwork and text scenes |
| Background build buttons / Turret sell buttons | `Runtime/AgeOfWar/AgeOfWarTwoWorldControls.cs`; original three-state child scenes, scaled hit zones and core Moon frame counters |
| MainMenu / Difficulty / Credits / Victory / Defeat TweenMax hover | `AgeOfWarTwoExposure.cs`, `Runtime/AgeOfWar/AgeOfWarTwoMenuEffects.cs`, `Resources/AgeOfWar/Exposure.shader` |
| Embedded text GlowFilter / DropShadowFilter / ColorMatrixFilter | `Runtime/AgeOfWar/AgeOfWarTextEffects.cs` and `Resources/AgeOfWar/TextEffects.shader`; whole-text mask, source filter order and parameters |

## Clock and ordering

The SWF header's 30 fps is not the battle update frequency. `Moon/Engines/Engine.calculate_enterFrameHandler` computes `(getTimer() - lastTime) / 1000` and passes **seconds** once per actual stage event. GameScreen requests 150 fps, while Moon animations explicitly run at 30 fps. `Step(elapsedSeconds, cameraPhase)` is the production contract: one actual stage callback, using the observed elapsed seconds multiplied once by game speed. It does not subdivide the elapsed time into synthetic 150 Hz steps. `SetFrameRate` accepts the original pause slider's 25–300 target range, while `Tick()` is a deterministic test convenience that calls `Step(1 / TickRate)`. The host supplies integer-millisecond clock differences as the original getTimer did, and discards elapsed background/navigation time. The speed button switches 1 / 2.

Each nonpaused actual stage callback preserves this source order:

1. HUD cooldown / construction. Construction first decrements a positive timer; spawning occurs on a later step entering the `else` branch. Queue reservation and payment happen when clicked.
2. All animations in registration order. Completion handlers may immediately deal damage, append projectiles or change animation; unused elapsed time is discarded when the label changes.
3. Units, including death recognition, population release and rewards.
4. Turrets, including target selection and cannon rotation.
5. Camera phase callback, before projectile/effect collision and audio. The host drains earlier sound events with the earlier camera position.
6. Projectiles.
7. Dynamic objects. Their manager snapshots the object count, so newly appended rain strikes wait until the next dynamic-object render; constructor-time lightning/laser damage still happens immediately.
8. AI. AI-created units receive their first unit update on the next step.

Units created by the HUD participate in the same step's animation and movement phases. A unit killed by a projectile or special releases its population on the next unit phase. Each manager removes dead objects on its next pass. Attack callbacks precede this frame's target/movement recalculation, as in Flash.

Native MovieClips `p4` (9 frames) and `tp5` (8 frames) are outside Moon's animation manager. They advance once per actual stage callback, including during the in-game pause, and are not multiplied by the 2× battle speed. The requested frame rate is a scheduling target, not a promise that a display can deliver that rate. Ending the battle stops both clocks. Host navigation/background suspension is handled outside this core.

Math.random has no public seed/replay contract in the original. The native port uses a seeded xorshift32 stream and consumes random samples at the original gameplay/sound-selection sites, including single-choice sound lists. This permits reproducible native tests; it does not claim bit-for-bit equality to an unseeded Flash session.

The `DisplayObject.x/y` setters truncate each write to integer twips (`truncate(value * 20) / 20`), including intermediate binary Number boundaries. Unit, projectile, special and damage-number positions use these setters; velocity, timing, scale and rotation remain ordinary numeric fields. This behavior was verified by an independent AS3 ABC added to the original SWF without changing either original ABC byte hash, then executed in Ruffle 0.7.1. The 16 observed x/y results, injector, script and source/bytecode hashes are in `Tests/AgeOfWarTwo/OriginalDisplayObjectProbe`. This probe validates display-coordinate assignment, not full game parity.

## Original edge behavior retained

- Enemy difficulty multiplies newly spawned **health only**. DamageManager does not apply a difficulty damage multiplier.
- Damage rounds the base random damage plus category upgrade, applies the four 1.5× type counters, then takes the ceiling. There is no counter multiplier against bases.
- Unit health/range are sampled at spawn; existing units are not rewritten by later purchases. Damage upgrades are read when damage occurs.
- Income is sampled using the *victim's side* when the victim spawns. Consequently player Income does not increase ordinary enemy-kill rewards; it increases the XP calculation for the player's own casualties. No correction was silently applied.
- DATA names `Turrets Damage` / `Turrets Range`; Turret requests `Turret Range`, and DamageManager has no `Turret` upgrade case. The original ineffective turret upgrades remain purchasable.
- `checkUnitButtonAvail` clears `unavailableUnits` on every button. Only the final visible unit remains blocked by Execute, while every requirement still controls its lock appearance/tooltip. The locked-looking Mage/Cannon can therefore be purchased in their original ages.
- Future HUD begins at unit 24 and ends at 29. Tank remains a visible underlying button, obscured by higher-depth ACS at the same position. The dynamic HUD respects that overlap.
- Queue capacity checks `sameIdWaitingCount > 4`, permitting **five** waiting entries of the same type. The currently building unit is excluded. Total population is 10; reservation occurs immediately.
- Special cooldown starts at 60 seconds. A small negative value becomes exactly zero on the following HUD step; only exact zero is ready.
- Evolution consumes XP and adds the difference between old/new maximum base HP, preserving previously sustained damage.
- Normal explosive projectile impact has cosmetic explosion only; no splash damage is invented.
- Projectile and special collision returns the **last** matching live opposing unit in insertion order. Normal projectiles use `sizeW * .6`; specials use full `sizeW`.
- Rain skills last 10 seconds; intervals are .2/.3/.3/.05/.2 seconds. Damage is 200/500/800/800/1000. Plane bombs do 600; laser does 1500. Arrows remain on the ground for four seconds. No special damages bases.
- Special6/7's uninitialized AS Number `timer` is NaN, so the unused `timer <= 0` test does not destroy them. The plane/ray traverses from ±1080, emits only between ±580 and exits at the opposite ±1080.
- Sold turrets have an empty source destroy method, leaving their registered animation. A pending firing callback may finish after sale; no extra repeat firing is started without the removed turret's render call.
- No SharedObject battle-save implementation exists in this release; no new save/resume semantics were inserted.

## Presentation contract

All coordinates use original Flash world space, Y positive downward, ground zero. GameScreen world Y is 390; base **collision edges** are −580 and +580. Raw base artwork has different registrations: player `(-491.3,100)`, enemy `(490.7,100)` mirrored. Tower extension sprites use the exact parent/child matrix (.75 scale, local x −130.5, y −243.75/−290.2/−336.6/−383).

Visual Frame is zero based **within the active label**, and Animation is the exact label, including `attackMeleeHit`, `walkAttackHit1`, `shootLaunch`. Labels are independent Moon animation segments, not merely hit event markers. Unlabeled artwork uses `idle`.

World draw order is background/bases, units, turrets, projectiles, dynamic objects. Turret base and cannon visuals share their manager depth so the stable presentation ordering preserves each turret's original child order.

Build crosses run at 60 fps from battle initialization; each sell cross runs at 30 fps from its turret's creation. Both original `move` clips have 30 frames and stay registered while hidden. `BuildTurretFrame` / slot `SellFrame` expose the zero-based source phase, following actual elapsed seconds, game speed and pause. Slot `Scale` preserves the turret animation parent's scale for its original 44×44 control hit zone. World-control hover/down states are extracted child scenes, so the moving cross is drawn once with the state's original colors and filters.

Keys: `unit/{id}`, `turret/{id}` (`base` is a separate stationary layer; cannon labels rotate independently), `projectile/p1`…`p7`, `projectile/tp1`…`tp15`, `special/1`…`7`, `special/6_plane`, `special/6_bomb`, `part/explosion`, `part/firetrail`, `base/{age}`, `base-extension/{slot}/{age}`. Bullet origins, direction and velocity are extracted from each exact source bp/bp2 matrix, including cannon-local scaling, not estimated from unit height or attack category.

CaptureTooltip returns the original field names and formatting: `nameTXT`, `costTXT`, `popTXT`, `hpTXT`, `walkTXT`, `typeTXT`, `damageTXT`, `hitSpeedTXT`, `buildTXT`, `rangeTXT`, `descTXT`, `levelTXT`, and `onT.costTXT` as appropriate. Original tooltips display XML base stats even after upgrades. Cost suffixes are ` Gold`, ` Xp`, and resale `$`.

`SetShowDamage` defaults to false as in GlobalVariables. Enabled damage numbers use the original two-second lifetime, shrinking upward velocity, alpha, integer values and critical-counter style through `TextScene` / `TextField` (`damage-normal` or `damage-critical`, `damageTXT`). Unit health displays expose their own alpha and original world-space placement. Projectile/special damage starts alpha at 2; it fades at 2 per second, while melee and `HoverUnit` hold it at 1. Displayed health follows the source unit-update phase, rather than being recomputed during Capture.

## Validation and remaining integration boundary

Run `scripts/test-age-of-war-two.sh` with .NET 10. It compiles the actual production core with C# 7.3 and warnings as errors. The first test was observed failing for the absent simulation; subsequent presentation, escrow and tooltip tests were observed failing before their implementation.

The suite covers all catalogs, requirements and source quirks; queue/population/cost/cooldown; all four difficulties and type counters; exact windup/recovery callbacks; all 29 unit and 17 turret attack paths; all 22 projectile collision and both-base paths; every special's distinct damage/lifetime/emission; AI progression through seven ages and final four-tower defense; deterministic repetition; source-driven victory and defeat; render registration and two-phase tower purchasing; tooltip values; configurable frame rate; damage-number timing and health-display reveal/hover/fade.

Passing these tests establishes source-contract and native simulation execution coverage. It does **not** establish Unity scene rendering, font/raster fidelity, audio playback, mobile input, lifecycle or device regression, nor a captured original-versus-native frame-by-frame full-game equivalence result. Those are host/asset integration validations. The runtime HUD helper's original-asset scenes must be present; missing artwork throws rather than silently drawing substitute geometry.

Dynamic text filters run on a render texture containing the complete generated glyph mesh. Filter colors, angle (radians), distance, strength, inner/knockout/composite flags, blur kernel widths and repeated passes are read from source metadata. Box-kernel and composition semantics were checked against the offline FFDec renderer's `Filtering.blur`, `dropShadow` and `compose`. This preserves the supplied filter operations; Unity glyph rasterization and GPU output still require visual integration checks against the original. Flash `useOutlines` means embedded glyph outlines, not a request for a black border.

Pause sliders preserve the source event sequence: only the original 12.5×12.5 knob bounds start a drag; quality/FPS knobs track pointer x continuously; displayed pending values update while dragging; the stage mouse-up applies volume and requested FPS before a subsequent CLICK. Reopening pause initializes the quality knob at its LOW/MEDIUM/HIGH detent. `AgeOfWarSliderChecks` replays down/held/up events through Page's production `ProcessPointer` path with the actual UI, assets and battle. The original broad-track drag behavior was observed failing that regression before the fix; its Unity green result belongs to the host validation report.

Menu hover follows TweenLite's default quadratic ease-out, with RGB offset 76.5/255 at exposure 1.3; the simultaneous `brightness` option is ignored by the source ColorTransformPlugin's earlier exposure branch. Hover takes .2 seconds; exit takes .5 seconds except victory/defeat (.2). These menu classes contain no scale tween. Dynamic text strips the original Flash HTML wrapper, preserves text/entities, applies original word-wrap flags and glyph letter spacing (1 px only for cash/damage). All exported fields have source leading 2 px and zero margins/indent; the embedded font layout now supplies baseline ascent/descent and field leading via a separate mesh effect. Unity measured windows/2 at a 13 px pitch while source metrics require 15 px (8 px text, ascent11, descent2, leading2); the first baseline similarly changes from9 to11 px. The real Unity regression includes blank lines and snapshot clones; await its current run before claiming runtime completion.

The follow-up original-runtime property fixture in `Tests/AgeOfWarTwo/OriginalDisplayPropertiesProbe` verifies alpha writes in signed 1/256 increments (without clamping 0–1) and rotation normalization to −180…180 with both exact boundaries retained. Core health/damage opacity and projectile/special/cannon rotations now preserve those setters. Seven tooltip windows retain independent opacity states; every actual stage update advances their source fade, including battle pause, while repeated presentation renders do not advance time. Switching tooltip types keeps the old window fading out alongside the new window. The window layer remains above the source pause panel.

Original runtime evidence: [training and movement differential](age-of-war-two-parity.md) replays 597 actual stage dt values against production code: 4,788 comparisons, zero mismatches. This bounded fixture does not claim full random combat parity.
