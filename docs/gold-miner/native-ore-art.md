# Gold Miner native ore supplements

Generated with the built-in `image_gen` tool, transparent background enabled, 2026-11-09. No CLI/API fallback. These two new assets supplement the original native Gold Miner artwork; existing gold, rocks, bags, moles, backgrounds and UI remain unchanged. TNT reuses `objects/item_dynamite`.

Style references inspected: `Assets/Resources/GoldMiner/objects/ore_rock_small.png` and `ore_gold_large.png`. Generated files were copied unchanged into the repository. Each is RGBA 1254×1254 with alpha range 0..255. Each Unity importer clones the existing single-sprite rock importer with unique GUID/sprite IDs, no mipmaps, 1024 max import size and the same Android format 47 override.

## Bone

Asset: `Assets/Resources/GoldMiner/objects/ore_bone.png`

Use case: stylized-concept. Asset type: a single transparent PNG sprite for a polished hand-painted 2D gold-mining game. Primary request: one old cartoon ivory bone unearthed from a mine, a recognisable long femur with rounded ends, slightly diagonal from lower-left to upper-right, entire bone visible, centred on square transparent canvas with 12% clear margin. Match the reference images shown: thick very dark brown outline, warm cream upper-left highlights, warm ochre/taupe lower-right shadows, angular painterly facets and subtle worn speckles, clean readable silhouette. It should look like the same artist painted the existing faceted gold nugget and gray rock sprites. The bone is a collectible obstacle, friendly arcade style, no horror or gore. No rocks, no dirt pile, no floor shadow, no background, no text, no border, no extra objects. Genuine alpha transparency around the isolated bone. Keep visual centre near canvas centre.

Raw nonzero-alpha bounds, Unity normalized bottom-left Rect: `(0.039074960, 0.021531100, 0.932216906, 0.954545455)`. Main visible silhouette at alpha >8: pixel bounds `(93,171)-(1169,1125)` measured top-left. Very faint generated fringe pixels exist outside the main silhouette; no postprocessing applied.

## Skull

Asset: `Assets/Resources/GoldMiner/objects/ore_skull.png`

Use case: stylized-concept. Asset type: a single transparent PNG sprite for a polished hand-painted 2D gold-mining game. Primary request: one small old cartoon ivory skull unearthed from a mine, roughly rounded silhouette, face in readable three-quarter view turned slightly left, whole skull visible, no jawbone detached, friendly arcade fossil prop rather than horror. Centre on square transparent canvas, 12% clear margin. Match the reference gold nugget and gray rock sprites shown earlier and the freshly generated bone: thick very dark brown outline, warm cream upper-left highlights, warm ochre/taupe lower-right shadows, angular painterly facets, a few fine cracks and subtle worn speckles, clean readable silhouette. Simple dark eye sockets and tiny nose opening, a few blunt cartoon teeth. No gore, no flesh, no threatening expression, no extra bones, no dirt pile, no floor shadow, no background, no text, no border. Genuine alpha transparency around the isolated skull. Keep visual centre near canvas centre.

Raw nonzero-alpha bounds, Unity normalized bottom-left Rect: `(0, 0.017543860, 0.976076555, 0.975279107)`. Main visible silhouette at alpha >8: pixel bounds `(186,194)-(1050,1049)` measured top-left. Very faint generated fringe pixels exist outside the main silhouette; no postprocessing applied.
