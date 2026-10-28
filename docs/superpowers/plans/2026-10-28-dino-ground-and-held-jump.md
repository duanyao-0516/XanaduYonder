# Dino ground synchronization and held jump

User authorized both fixes and an OPPO overwrite installation. Continue in the existing isolated Dino worktree.

The current rules remain unchanged: fixed 60 Hz, original speed acceleration and jump physics, original obstacle generation, and separate orientation records. Ground travel must accumulate the same rounded step as stationary cactus obstacles, including the fractional collision step; score distance remains unrounded. Keyboard auto-repeat in Chromium is event-driven. For consistent mobile operation, the page will retry a held jump at the first grounded simulation step. Core Jump remains a single command. Releasing, exiting/canceling the pointer, suspending, changing pages or ending the run clears repeat intent. An airborne press can become a held input, but an airborne tap is not buffered after release. Duck has priority.

- [x] Ground: first reproduce relative drift with Core and real UGUI position checks; add GroundDistance to DinoGame and use it only for RenderGround. Cover acceleration, both viewports, collision contact, freeze and reconstruction.
- [x] Held input: add failing DinoPageValidation cases through the actual Page Update and pointer callbacks. Separate owner registration from TryJump, then run TryJump before each fixed simulation step while held. Play jump sound only on an accepted jump; retain pause and input ownership.
- [x] Verify: run core, session and production Unity page/ground checks serially; inspect text layout if help changes. Obtain independent review.
- [x] Deliver: Android build and OPPO overwrite install completed; saves match and cold launch succeeds. User independently tested and reported no issue, then requested the next direction-transition fix. Agent device checks were limited to install/save/launch; held input and both orientations were verified in Unity.

Speed reference for current OPPO layout: portrait nominal393.6, initial actual cactus324; landscape486; both maximum1053 world units/s. Acceleration caps at8141ticks/135.6833s portrait and7000ticks/116.6667s landscape. Ground and cactus visible speed uses integer source pixels per tick; flying birds deliberately retain their original speed offset.
