# Bomb It full-height controls implementation plan

**Goal:** Apply the user's specified layout: enlarge the intact 550×400 game, move toolbar actions into the side panels, use person/group/pause/book/settings icons, and allow persistent joystick/directional-button selection.

**Architecture:** Only the existing web adapter changes. HTML/CSS own side-panel geometry and icon presentation; host.js owns pause/modal lifecycle; controls.js owns input and its separate preference. The original SWF, Ruffle and native hosts remain unchanged.

**Tech stack:** HTML, CSS, JavaScript, existing Ruffle wrapper, existing Chrome smoke runner, unified Android build.

- [x] Replace `ThirdParty/BombItContent/adapter/index.html` toolbar with left/right tool groups. Keep existing element IDs, add settings and two `.dpad` groups. Direction buttons explicitly carry `data-player` and `data-action`; original joystick and bomb selectors remain stable.
- [x] Change `style.css` to a full-height three-column landscape layout. Fit the center at original aspect ratio, align its bottom to the viewport, reserve cutout/gesture insets for side controls, and keep all hit targets outside the original stage. Preserve a usable portrait fallback.
- [x] Change `host.js` to show settings through the existing modal/pause boundary; settings calls `controls.getStyle()` and `controls.setStyle('joystick'|'dpad')`. The pause button switches its SVG state and accessible label, keeping its DOM icon. Close settings restores the previous pause state.
- [x] Change `controls.js` to maintain one independent validated preference, defaulting to joystick. Switching input style releases all held keys; directional-pad slide, cancellation and multi-touch use the existing key-ownership boundary. Storage failure must not block gameplay.
- [x] Build content with `python3 scripts/bomb-it/build-content.py`; run the existing `check-browser.cjs` against the actual SWF with `PLAYWRIGHT_MODULE` set to the installed module. Cover both control styles, persisted preference, settings pause/resume, dual-player multi-touch, safe-area/phone/tablet layout, no overlapping stage, full-height gain and bottom alignment.
- [x] Independently review the adapter, inspect actual screenshots, update `docs/bomb-it.md`, then build Android with `./scripts/build.sh android`. Replace the existing Debug app on the authorized OPPO if connected; no device gameplay automation. Do not build or install iOS in this iteration.

User-provided design is the working specification. No new native permission, network path, SWF rule change or unrelated game refactor is included.
