# Aircraft Fire APK reference

The user supplied `Aircraft+Fire_2.5_APKPure.apk` locally. Android package metadata identifies `czh.aircraftwar`, version name `2.5`, version code `11`. SHA-256:

```text
052519b7cd43722a99eb079a349fb85b2218234c7e77594a8125e2a1160ed142
```

The original game uses compiled Cocos2d C++, not Unity C#. The port uses new C# gameplay code and the gameplay art/audio extracted from that APK. No original executable, advertising library, purchase code, notification code, account integration, online leaderboard, or recommendation link is imported. This document describes static recovery from exported native symbols, Thumb disassembly, localization, and resources; it is not a claim of device-verified equivalence.

## Asset reproduction

With Python 3 and Pillow installed:

```sh
python3 scripts/extract-aircraft-fire-assets.py /path/to/Aircraft+Fire_2.5_APKPure.apk
```

The script verifies the exact APK hash before extracting. It restores rotated and trimmed sprites to their original dimensions and writes deterministic Unity single-sprite metadata. [aircraft-fire-assets.json](aircraft-fire-assets.json) records all 43 imported files, their source entries, dimensions, and SHA-256 values.

- `Logo.png` is the actual application icon declared by the APK manifest: `res/mipmap-xhdpi-v4/icon.png`, 96×96. The unrelated `ic_launcher.png` files are not the application icon.
- `Title.png` is `assets/logo.png`, 512×128.
- `Sprites/background_2.png` is 640×1136. The source atlas is 1024×2048.
- The standalone `assets/enemy_bullet.png` is 12×12. The plist's `enemy_bullet.png` entry incorrectly points at an empty atlas region and is deliberately replaced with this original standalone resource.
- Only the five gameplay MP3s are imported: background music, boss arrival, boss destruction, enemy destruction, and player destruction.

| Resource | Original canvas | Meaning |
| --- | --- | --- |
| `hero_fly_1/2` | 136×168 | Two player flight frames |
| `hero_blowup_1..4` | 136×168 | Player destruction |
| `enemy1_fly_1` | 76×56 | Small enemy, 4 destruction frames |
| `enemy3_fly_1` | 92×132 | Medium enemy, hit frame and 4 destruction frames |
| `enemy2_fly_1/2` | 225×344 | Large classic enemy / stage boss, hit frame and 7 destruction frames |
| `enemy4_fly_1` | 79×141 | Red parachute bomb pickup, despite its misleading name |
| `enemy5_fly_1` | 76×116 | Blue parachute double-shot pickup |
| `bomb` | 84×76 | Bomb inventory icon |
| `bullet1` | 12×28 | Normal orange projectile |
| `bullet2` | 96×28 | One wide sprite depicting two blue projectiles |

## Recovered rules

Native symbol addresses below refer to `lib/armeabi/libgame.so` in the supplied APK. Local inspection used Unity Android NDK `llvm-nm -D --demangle` and `llvm-objdump -d --demangle --triple=thumbv7`. The binary is not stored in this repository.

- The localized menu exposes Classic Mode and Level Mode. Normal/Crazy is a classic-mode toggle: `GameLevel::setCrazyAttack(bool)` at `0x159f26` returns immediately; `GameLevelClassic::setCrazyAttack(bool)` at `0x15a0b6` changes classic rates.
- Touch movement is relative drag, using current minus previous touch location: `GameScene::ccTouchesMoved` at `0x15be88`.
- Initial/restart player lives are 3, and initial bomb inventory is empty: `AircraftHero::init` at `0x159418`, `restart` at `0x1598c4`.
- Player automatic fire runs every 0.2 seconds, projectile speed 1500 design pixels/second: `setFireEnabled` at `0x159874`, `fire` at `0x1594e4`.
- The double-shot pickup supplies exactly 90 wide projectiles; replacement refreshes it rather than stacking shots: `WeaponBullet::getBullet` at `0x15f1b8`, `AircraftHero::pickup` at `0x159834`. The original wide projectile has power 1 and one 96×28 bounding rectangle.
- Small/medium/large classic enemies have HP 1/3/9 and scores 1000/6000/30000: class A/B/C init methods at `0x160194`, `0x16017c`, `0x160164`; score methods at `0x15fc38`, `0x15fc40`, `0x15fc48`.
- Stage small/medium enemies have HP 1/3 and scores 1000/6000. The large silhouette is used for stage bosses.
- Bombs destroy ordinary enemies and damage a boss by 30 HP: `AircraftEnemy::bomb` at `0x158238`, `AircraftEnemyBoss::bomb` at `0x158248`.
- Pickups descend at 150 pixels/second. Their visual swing rotates +15 degrees over 0.4 seconds then alternates -30/+30 degrees over 0.8 seconds: `Weapon::falldown` at `0x15f008`.
- Player collision uses the union of two inset rectangles, relative to the player's center: 42×100 at (0,24), and 100×32 at (0,12), rather than the full 136×168 art. Enemy/projectile collisions use their bounding rectangles: `AircraftHero::isAttack` at `0x159724`, `GameScene::update` at `0x15d484`.
- `AircraftHero::revive` at `0x159968` restores 3 lives and blinks ten times over 2 seconds. The APK's advertising-related revival gate is excluded from the offline port.

### Classic progression

`GameLevelManager` at `0x15afe0` initializes classic values; `GameLevelClassic::generate` at `0x15a574` consumes them. A range `(base,length)` means `base + U × length`, not minimum/maximum, where U is a random value from zero to one.

| Spawn | Interval range, seconds | Speed range, pixels/second | Timeline adjustment |
| --- | --- | --- | --- |
| Small | (0,1.5) | (250,500) | Interval base −0.75f; speed base +500f |
| Medium | (0,5) | (250,400) | Interval base −3.5f; speed base +400f |
| Large | (10,20) | (100,200) | Interval base −16f; speed base +200f |
| Double shot | (30,60) | 150 | None |
| Bomb | (60,120) | 150 | None |

Here `f=min(elapsed/600,1)`. If a shifted interval base becomes negative, the negative part is added to length, base becomes zero, and length is clamped to at least 0.25. Crazy halves the three enemy interval bases and lengths and doubles their speed bases **before** applying timeline adjustments; the timeline adjustment constants do not change. Pickup intervals are unchanged. Classic enemies travel vertically and do not fire.

### Seven-stage progression

The stage table is initialized in `GameLevelManager` at `0x15afe0`; `GameLevel::generate` at `0x15aa58` generates the boss once the listed duration is reached. Ordinary enemies continue while it is present.

| Stage | Boss arrival (s) | Enemy speed (base,length) | Small interval | Medium interval | Double interval | Bomb interval | Boss HP | Burst count | Fire delay length |
| --- | ---: | --- | --- | --- | --- | --- | ---: | ---: | ---: |
| 1 | 60 | (250,300) | (0,2.5) | (4,10) | (30,50) | (50,120) | 300 | 2 | 4.5 |
| 2 | 60 | (300,300) | (0,2) | (3,10) | (30,50) | (50,110) | 400 | 3 | 4 |
| 3 | 90 | (350,300) | (0,2) | (2,8) | (30,45) | (40,100) | 500 | 3 | 3.5 |
| 4 | 90 | (400,300) | (0,1.5) | (1.5,5) | (20,30) | (40,90) | 600 | 4 | 3 |
| 5 | 90 | (450,300) | (0,1.2) | (1,4) | (20,30) | (30,70) | 700 | 4 | 2.5 |
| 6 | 90 | (500,300) | (0,1) | (0,3) | (10,30) | (30,60) | 900 | 5 | 2 |
| 7 | 120 | (600,300) | (0,1) | (0,2) | (7,30) | (20,60) | 1000 | 5 | 1 |

For stage enemy speed and spawn intervals, `f=min(elapsed/stageDuration,1)` and `delta=length*f*0.5`. Speed adds delta to base and subtracts it from length. Spawn intervals keep base and subtract delta from length. Pickups do not shift.

Stage small enemies independently choose easing and a curved side exit with probabilities `[.2,.3,.3,.3,.4,.5,.5]`. The curve ends at x=0 or screen width, y=random×half screen height, from a normal top spawn. Its two cubic controls are `(startX,(startY+endY)/2)` and `((startX+endX)/2,endY)`. Duration is straight-line distance divided by speed. Easing chooses `round(1+2U)`: Cocos EaseIn, EaseOut, or EaseInOut with rate 0.5. Source: `AircraftEnemyA::attack` at `0x158c08`.

Stage medium enemies fire once after traveling a random 0–70% of the full downward journey. That shot emits `round(2U)+1` simultaneous bullets, each aimed at a random y in the bottom 70% of the screen on the opposite horizontal edge. Projectile speed is 200 pixels/second; these projectiles can travel upward or downward depending on the target. Source: `AircraftEnemyB::attack` at `0x158788`, `fire` at `0x158ef0`.

Bosses enter downward by their full height plus 5 pixels at 100 pixels/second, then move horizontally at 100 pixels/second between their half-width bounds. Movement pauses for each burst, whose shots are separated by 0.3 seconds. Each shot travels straight down at 200 pixels/second. On resuming movement, fire delay is chosen as `1+U*fireDelayLength` and initial movement direction is randomized. Source: `attack` at `0x1588f8`, `move` at `0x1585a4`, `shot()` at `0x158dd4`, `shot(int)` at `0x1590c8`.

### Native quirk and validation limits

The native boss `getPoints()` at `0x158224` returns **current HP × 100**. `AircraftSprite::hit` at `0x159af2` reduces and clamps HP to zero before `GameScene::update` queries score on a killing bullet, so that path awards zero boss points. A fixed initial-HP-based boss reward would be a deliberate gameplay correction, not a recovered native rule. Static recovery also does not establish the exact original device scaling or input feel; those require separate runtime comparison.

The offline C# integration preserves the original double-shot resource as **one 96×28 projectile with power 1**, including its wide collision rectangle. It deliberately awards boss maximum HP×100 consistently, permits one free offline revival per run instead of an advertising gate, and grants 3 seconds of protection after an ordinary life loss rather than the native 2-second blink following its 0.4-second destruction animation. These latter changes are explicit port decisions, not claims about the native behavior.

## Offline record contract

The C# save files are independent of the APK's original storage. Schema 1, `gameId=aircraft-fire`, keeps separate classic/crazy/level Int64 scores as decimal strings and highest reached stage (0 means no recorded level run, otherwise 1–7). Overall best is derived. The codec rejects duplicate, missing, unknown, null, wrong-type, negative, overflowing, or oversized records. Loads are read-only and combine maxima per field from valid primary/backup files. Saves reject downgrades, preserve prior maxima in the backup, and replace each file atomically. Unreadable records cannot be overwritten by zero-valued UI defaults.

Verification: `bash scripts/test-aircraft-fire-persistence.sh` exercises actual encoding and temporary-directory disk recovery. Asset verification checks the recorded SHA-256 and image dimensions for every imported file. Neither check claims Unity rendering, Android runtime, or touch parity.
