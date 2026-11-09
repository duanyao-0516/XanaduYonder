# Bomb It edge input and compact UI corrections

**Goal:** Restore the full visible touch controls and apply the user's toolbar, spacing, continue-label and help-dialog corrections.

**Architecture:** Keep input ownership in `adapter/controls.js` and presentation in `host.js` / `style.css`. Native insets describe system gesture priority, not a blanket ban on delivered taps. Preserve the existing SWF, checkpoint and feedback contracts, stage size and phone-verification boundary.

- [x] Reproduce visible edge-control failure with the delivered content and nonzero gesture insets; verify native event/inset ownership independently.
- [x] Correct input priority at the shared pointer-entry boundary. Cover single/duo, pad/stick/bomb, free gesture starts and cancellation.
- [x] Make the three right tools a smaller single row; separate the left brand and mode group. Narrow settings; place the help close action after its content; include difficulty in the continue entry.
- [x] Inspect real browser screenshots at normal and short landscape sizes, and run the focused input and feedback integrations. Final content: 9 short + 6 gesture-edge + 9 feedback/UI checks, no page errors; independent source/layout review complete. No phone operation.
- [x] Rebuild Android through the existing unified script and verify bundled content. Exit 0; signature and all 14 actual Bomb It payload files passed. Latest user instruction authorized direct installation: `adb -s 3B6F4NE8A66ZXTRL install -r` returned `Success`. No uninstall, data clearing, game launch or interaction, commit, merge or publication.

Delivered APK: `artifacts/builds/XanaduYonder-0.9.7-debug.apk`, 233,724,544 bytes, SHA-256 `08b2a81f7623b293b424eccebdd1bf91bea2f7c44c07e448b5a7482d2d6b1c85`. Package remains `com.duanyao.xanaduyonder.debug`, version 0.9.7/build16.
