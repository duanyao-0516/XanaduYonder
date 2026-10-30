# Original AVM2 text metrics and bounded raster probe

Captured in Ruffle0.7.1 on 2026-10-30 at08:41:01.962UTC, with a separate GameScreen subclass ABC. Both original ABC hashes are retained. Source: `scripts/age-of-war-parity/two/text/probe.as`; this fixture is independent of the597-frame training/movement differential.

`reference.txt` includes original TextField properties, `getLineMetrics` results, and ARGB pixel readback from `BitmapData.draw`. PNGs are lossless conversions of those readback bytes; `pixels.json` records RGBA hashes and alpha counts.

At original size8px, HUD money/error and WINDOWS cost report normal/pixel, ascent11, descent2, leading2, line height15. Multiline WINDOWS reports first-character bounds at y0,15,30. Original damage at14px reports advanced/pixel, ascent10.75, descent3.2, leading2, line height15.95. This proves fractional metrics use truncated signed twips; rounding descent would incorrectly produce3.25 and height16.

Raster scope is a fixed160×40 transparent ROI with identity transform and text123456. Each normal-font sample has the same pixel hash at LOW/MEDIUM/HIGH and no partial-alpha pixels. Damage has distinct hashes and0/139/187 partial-alpha pixels respectively. Some text extends beyond the ROI, including the right side of damage; these are **ROI observations**, not full-field/stage equivalence. No0.25px phase capture was run. The window-M raster is empty because its right alignment puts the glyphs beyond this ROI after its width becomes200; do not treat it as baseline-pixel evidence. The artificial TextFormat size14.05 setter reads back14; this is not an original asset font size.

To reproduce, use the same injector preparation procedure as `docs/age-of-war-two-parity.md`, substituting this directory's `text/probe.as` and HTML harness. The PNG dimensions/readback interpretation are fixed160×40 ARGB32. Do not substitute the later unexecuted `/tmp` phase experiment for this checked-in source.
