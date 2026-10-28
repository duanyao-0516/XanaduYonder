# Sheep difficulty separation implementation plan

> **For agentic workers:** Use subagent-driven-development; root owns production generation, diagnostic agent owns independent gameplay benchmarks, prototype agent owns ignored experiments, final reviewer reviews the actual diff.

**Goal:** Make all four difficulties meaningfully distinct; Extreme should have very low tolerance for mistakes and demand repeated attempts and thoughtful tool use, per user feedback.

**Architecture:** Keep the real 144-card/7-slot game rules, 8/12/16/16 kind balance, existing geometry and frozen tool quotas. Replace witness-pressure-only tuning with dependency-aware constructive generation and bounded candidate evaluation. Every returned board retains a verified no-tool legal witness. Simulated players never read that witness. The generous observation contract knows every ordinary covered icon but masks hidden blind icons; exposed observation masks every covered icon.

**Tech Stack:** C#, Unity 6000.3.27f1, .NET source tests and deterministic seeded simulations.

## Evidence and acceptance

Baseline seeds 1..100: visible greedy with standard tools wins Easy97, Normal85, Hard66, Extreme64. Without tools:73/50/28/21. A second visible three-step strategy without tools wins76/45/33/30. These are deterministic bot outcomes, not human win rates. Old pressure tests only replayed the supplied witness and missed alternative easy paths.

Root causes: global random topological removal exposes many interchangeable choices; a five-open-kind/six-slot assignment repeatedly puts the second and third matching tile adjacent in the witness, allowing cheap visible triples. A calibration must evaluate independent choices and standard tools as well as witness legality.

- [x] Persist visible greedy/limited-lookahead players and a fixed seed regression and separate unseen-seed benchmark under `Tests/SheepDifficulty`; observe existing implementation fail a meaningful separation regression.
- [x] Prototype dependency-focused removal, separated second/third matches, and balanced group coloring that penalizes concurrently selectable duplicate kinds. Compare bounded candidate selection and record generation timings before choosing production parameters.
- [x] Implement selected generator profiles in `Core/Sheep` with bounded work and deterministic RNG. Keep actual snapshot/geometry and saved-game contracts unchanged. Every published result must replay through actual `SheepGame.Pick` to victory without tools.
- [x] Replace the obsolete monotonic-witness-pressure test with independent difficulty evidence. Retain kind totals, deterministic generation, all-layout coverage and valid witness checks. Check held-out seeds and a second policy, including standard quotas.
- [x] Run core, layout, persistence, session and presentation regressions; independently review generator and benchmark information boundaries. Build the isolated sheep validation APK, time actual new-game generation on the test device and verify new runs show correct difficulty/quotas.
- [x] Update gameplay/verification docs with precise algorithms, measured bot outcomes, runtime limits and limits of human-difficulty claims; commit locally.

## Scope

No quota reductions, larger tile counts, smaller tray, app branding edits, changes to other worktrees, old-save migration, remote push or public release. Existing saved boards remain playable; the new generator applies to newly created runs/waves. Default-tools benchmarks matter because a no-tool failure is often cheaply recoverable in the actual product.

## Final implementation and scope notes

Easy uses the focused legal order with consecutive triples; Normal and Endless
retain their original generation behavior. Hard uses dependency-focused groups
and balanced coloring. Extreme evaluates 16 candidates against greedy, three-pick
beam-eight planning, and exposed-only greedy players with standard tools. The
maximum cleared count is ranked first, then the sum, then opening clusters.
The independent beam-twelve strategy is reserved for cross-validation.

The fixed regression succeeds at 100/86/36/2 percent. Unseen seeds 2001..2100
are reported separately; the Normal greedy completion is 91 percent, just outside
the fixed regression's provisional 50..90 percent band. No parameter was changed
after inspecting these unseen results. See verification-sheep-difficulty.md for
all strategies, device timing, local delivery, and limits of inference.
