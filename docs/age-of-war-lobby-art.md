# Age of War lobby artwork

The small lobby icon remains the existing `Assets/Resources/Lobby/Icons/age-of-war.png` from master.

`Assets/Resources/Lobby/AgeOfWarLogo.png` contains the original transparent **AGE OF WAR** title, without a sequel numeral, menu labels, or sponsor artwork. It is a direct FFDec PNG export of the original vector shape, copied unchanged into the lobby resources. The PNG is 536 × 57 pixels; the lobby preserves its aspect ratio.

- Original SWF: `age-of-war-1-7k7k.swf`
- Original SWF SHA-256: `5f88517846ff0eb75eee3bd6dfebbf9fad75edbb4dbe20ed8ea4c0afb3dfd3a1`
- Source: DefineShape character 1013, also placed inside the original menu sprite 1017
- Archived original vector: `scripts/age-of-war/source-art.zip` → `aow1/export/shapes/1013.svg`
- Archived SVG SHA-256: `8fadfbb08d26e8dea2446590e8e8f52a308f6cb457e1fef5521c6bdce0a60de9`
- Exported PNG SHA-256: `babf364516d24b449a80f9f4e0a6a45b1487564ef3b601e53ad0beca99d9b10d`
- Converter: JPEXS FFDec 26.4.0, Java 17; default 1× shape rendering

Reproduction command (substitute local tool and original-source paths):

```sh
java -Djava.awt.headless=true -jar ffdec.jar -format shape:png -selectid 1013 -export shape /tmp/aow-lobby-title age-of-war-1-7k7k.swf
cp /tmp/aow-lobby-title/1013.png Assets/Resources/Lobby/AgeOfWarLogo.png
```

No cropping, recomposition, recoloring, or AI generation was performed. The title retains its original geometry, colors, and transparent background. It is imported as a single Sprite at `Lobby/AgeOfWarLogo`, independent of the game atlas loader and its disposal lifecycle. The single-card preview composes a 90 × 90 UGUI badge using the existing cream/green palette: the original green crossed-swords icon forms the main mark, with this gold title on a dark-green band below. The existing LobbyArtwork layout fits the complete badge into the shared 63 × 63 slot. Both Sprite assets keep their native aspect ratios; no texture is recomposed or modified, and the common lobby layout is unchanged.
