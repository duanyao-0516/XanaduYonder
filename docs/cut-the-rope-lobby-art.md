# Cut the Rope lobby artwork

The compact icon uses the same pinned Lucide 1.17.0 package and rendering conventions as the other lobby icons. Scissors identify the rope-cutting action at the 20×20 display size; the former multicolour primitive preview is no longer used as a compact icon.

- Runtime sprite: `Assets/Resources/Lobby/Icons/cut-the-rope.png`.
- Official source: `docs/design/lobby-v2/additional-icons/source/scissors.svg`.
- The coloured SVG changes only `stroke="currentColor"` to `#276653`; official paths, 24×24 viewBox, 2-unit stroke, round caps and round joins are unchanged.
- Export: sharp 0.35.5 at density 288, 96×96 RGBA/sRGB PNG with transparency. Unity uses Sprite/Single, Full Rect, no mipmaps/compression, Clamp/Bilinear and no NPOT rescaling.
- Source, export and runtime hashes are recorded in `docs/design/lobby-v2/asset-map.json`; the existing Lucide ISC license applies.

The single-column large logo is the 72×72 PNG supplied by the user on 2026-11-01. `Assets/Resources/Lobby/CutTheRopeLogo.png` is copied byte for byte, without cropping, masking, background removal or re-encoding. Its SHA-256 is `71085dbe0a37a6770e5b513a2437a36fa16019f2494395992cea799825d41021`. It is imported as an uncompressed Sprite and displayed with `preserveAspect`, through the existing lobby artwork sizing path. The supplied bitmap's resolution is retained.

No gameplay state, WebView content or game asset lifetime is involved in either lobby image. Runtime Unity rendering and lobby checks are performed by the integration task.
