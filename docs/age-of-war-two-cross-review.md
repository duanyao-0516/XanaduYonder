# Age of War 2 independent lifecycle review, 2026-10-30

Reviewer: the One port worker, who did not implement Two. This pass read the original AS3 sources and native code directly, then ran an independent AS3 display-property probe inside a temporary copy of the original Two SWF. It did not treat the existing native tests as original-runtime evidence and did not edit Two production files.

Source paths below are relative to `analysis/aow2/outer/scripts/net/louissi/`. Native paths are relative to `Assets/Scripts/Core/AgeOfWar/Two/`. Line numbers refer to the reviewed snapshot before the Two worker's follow-up corrections.

## Confirmed issue: display-coordinate assignments retain excess precision

`Alpha1/units/Unit.as:13,643-645` moves the `MovieClip.x` property. `Alpha1/Particules/Projectile.as:8,119-120,194-195` assigns and increments `MovieClip.x/y`. In contrast, `AgeOfWarTwoUnits.cs:24,68` and `AgeOfWarTwoProjectiles.cs:23,27,38` store these results directly in ordinary doubles.

An additional independent ABC in the original `age-of-war-2-7k7k.swf` created a `Sprite` and read back x/y after writes in Ruffle0.7.1. At UTC07:37:17.259–260, actual console output included:

```text
AS3TWIP,0.025,0,0
AS3TWIP,0.049,0,0
AS3TWIP,0.099,0.05,0.05
AS3TWIP,-0.099,-0.05,-0.05
AS3TWIP,150.79999999999998,150.75,150.75
AS3TWIP,152.14999999999998,152.1,152.1
```

Columns are Number expression, Sprite.x, Sprite.y after the prefix. The observed rule is `truncate(value * 20) / 20`, including binary floating-point error before truncation. This validates the AS3 path independently; it is not an inference that AVM1 and AVM2 must behave alike. Ordinary speed, mass, dt and HP variables must retain Number precision. Rotation, scale and width were not probed.

The source/fixture/injector hashes,16-row CSV, AS3 and Java injector are in `/tmp/aow-parity-two-probe/`, handed to the Two owner for permanent parity-fixture archival. Both original serialized ABCs are retained, hashes `00eff07c31dd0a4d88c99e2294d20fc63821a058fc0e62f2c9a3f384e4b47fc6` and `4af9d8f674ae0a8a15d4862f79fac7fd4daa361a7aec5a8a54f1e6cf3f4f764e`. The injector only adds a new ABC. The source game methods are not decompiled/recompiled. The first attempted ExternalInterface channel failed with runtime Error2067 during eager script initialization; the successful observation used `trace`, Ruffle `logLevel=info`, and browser console logs. This probe is property-setter evidence, not a Two battle trace or Adobe Flash validation.

## Lifecycle paths inspected without a new confirmed discrepancy

| Path | Original evidence | Native evidence | Result |
|---|---|---|---|
| GameScreen → player training → animation → units → projectiles → AI | `Alpha1/Screens/GameScreen.as:94-129`; `Alpha1/Hud/Hud.as:202-231` | `AgeOfWarTwoBattle.cs:165-189` | Ordering matches. The original calls HUD while paused, but both its special timer and training are internally pause-gated. Player creation occurs before the animation pass; AI creation occurs after all actor managers. |
| Unit construction → animation stop callback → combat/death | `Alpha1/units/UnitsManager.as:49-53`; `Unit.as:90-163,330-424,694+`; `Moon/Animations/Animation.as:33-65,85-124` | `AgeOfWarTwoUnits.cs:28-40,80-113`; `AgeOfWarTwoAnimation.cs:33-48` | AS3 `new Unit` initialization is synchronous, unlike One's AVM1 attachMovie. The port resets clocks on label changes and invokes the stop callback at the final frame. No evidence supports applying One's deferred initialization here. |
| Projectile created by animation → movement/collision → next-pass removal | `Alpha1/units/Unit.as:458-468`; `Alpha1/Particules/Projectiles.as:16-45`; `Projectile.as:191-242` | `AgeOfWarTwoUnits.cs:122-129`; `AgeOfWarTwoBattle.cs:182-185`; `AgeOfWarTwoProjectiles.cs:36-46` | New projectiles join the same frame's projectile pass. A hit marks the object dead but leaves its display until the following manager pass. Native preserves that boundary. Collision returns the last matching unit in insertion order, as the source does. |

A suspected turret-animation leak was explicitly rejected as a port finding: source `Alpha1/Turrets/Turret.as:402-404` has an empty `destroy()` and therefore also leaves registered animations alive after sale. It must not be “fixed” based on normal lifecycle expectations without changing the source target.

These are bounded review results. No all-unit attack, variable-dt, pause, terminal-base or projectile battle differential run was completed in this review. The coordinate finding is handed off for source-backed native regression and correction by the Two owner.
