# Third-party notices

The project's original code is licensed under MIT. Third-party software and assets retain their own licenses.

## Noto Sans CJK SC

- Source: https://github.com/notofonts/noto-cjk/tree/main/Sans/OTF/SimplifiedChinese
- File: `Assets/Resources/Fonts/NotoSansCJKsc-Regular.otf`
- License: SIL Open Font License 1.1, reproduced in `Assets/Resources/Fonts/OFL.txt`.
- Copyright notices and the reserved font names are preserved in that license file. The font is bundled unchanged.

## Unity packages

Unity Editor, player runtime, UGUI, IDE integration and testing packages retain the terms and notices distributed with Unity and each package. Resolved versions are recorded in `Packages/packages-lock.json` after Unity imports the project.

`com.unity.nuget.newtonsoft-json` 3.2.2 packages Newtonsoft.Json 13.0.2 (MIT, James Newton-King). The package's included LICENSE/Third Party Notices apply. The standalone persistence tests reference the matching NuGet version.

All gameplay code, UI layout and synthesized sound generation in this repository are original. No implementation or artwork from another 2048 game is included.

## User-supplied jump assets

- Source archive: `跳一跳素材.zip`, supplied by the project owner on 2026-10-26.
- The project owner explicitly confirmed that free redistribution permission has already been obtained for these materials.
- Included unchanged: `Assets/Resources/Jump/Audio/{scale_intro,scale_loop,start,success,pop,fall}.mp3` and `Assets/Resources/Jump/Preview/{platform,pawn}.png` (original archive paths `sound/` and `img/boxs/box/box0.png`, `img/tyt/juese.png`).
- These assets retain their original rights; the repository's MIT license applies to original code and does not relicense these supplied assets. The archive did not include a separate license text or author attribution.
- The 3D meshes, rendering shader, gameplay and integration are original. The reference repository `dee1024/jump-jump-game` was inspected locally, and no code or assets from that repository are bundled.

## User-supplied tile assets (旸了个旸)

- Source archive: `yang_le_ge_yang_all_in_one.zip`, supplied by the project owner on 2026-10-26.
- Included unchanged: `Assets/Resources/Sheep/Tiles/block_1.png` through `block_16.png` and `block_bg.png`, from the archive's `all_in_one/block/theme0/` directory. Only these 17 required tile images are bundled.
- The enclosed `IP免费授权.pdf` excludes game uses and its term has expired. It is **not** the authorization basis for this use. After that restriction was specifically explained, the project owner separately confirmed authorization covering game development and distribution with the installation package: “已明确获得游戏开发的授权，进行完整开发工作” (2026-10-26).
- These images retain their original rights and are not relicensed under the repository's MIT license. The separate authorization confirmation is specific to this project; this notice does not grant independent reuse rights to others. No email template, personal information, or unrelated asset collections from the archive are bundled.
- The displayed game name is 旸了个旸. Game rules, level generator, interface layout, storage, and synthesized audio are original code; no third-party game implementation is copied.

## Project owner app logo

`Assets/Branding/XY_logo.png` is the unchanged `XY_logo.png` supplied by the project owner on 2026-10-26 for the app icon and launch branding. It is used as supplied; Unity produces the platform-specific icon sizes. The repository's code license does not independently grant rights to the project's visual identity.

## Sudoku Exchange puzzle data

- Source: https://github.com/grantm/sudoku-exchange-puzzle-bank at commit `d8c8ebaee0c08c412cfba96af1923dfa61c83317`.
- License: Unlicense, reproduced in `ThirdParty/Sudoku/LICENSE.txt`.
- Bundled file: `Assets/Resources/Sudoku/puzzles.txt`, a selection of 2,000 standard 9×9 puzzles with locally computed answers and difficulty labels. Import provenance and verification are documented in `docs/sudoku-content.md`.
- Only puzzle data is reused. The Sudoku rules, solver, hint explanations, interface and persistence code are original. No Sudoku Exchange web application code, QQWing or HoDoKu code is bundled.
