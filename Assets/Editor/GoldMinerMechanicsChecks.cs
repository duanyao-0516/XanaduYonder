using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.GoldMiner;
using XanaduYonder.Runtime.GoldMiner;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Presentation checks drive render snapshots in real gameplay units, without
    // advancing gameplay or touching a player's persistent save directory.
    public static class GoldMinerMechanicsChecks
    {
        private static readonly string Output = Path.GetFullPath("artifacts/gold-miner-development/mechanics");
        private static readonly List<string> Failures = new List<string>();
        private static readonly StringBuilder Report = new StringBuilder();
        private static int checks;

        public static void Run()
        {
            Failures.Clear(); Report.Clear(); checks = 0;
            Directory.CreateDirectory(Output);
            try
            {
                using (var fixture = new Fixture())
                {
                    CheckWholeMinerContract(fixture);
                    CheckHooks(fixture);
                    CheckWinches(fixture);
                    CheckRope(fixture);
                    CheckRopeTravel(fixture);
                    CheckHeadClearance(fixture);
                }
                using (var fixture = new Fixture()) CapturePhases(fixture);
                using (var fixture = new Fixture(mode: GoldMinerMode.Solo)) CapturePhases(fixture);
                CheckHeldObjectGrip();
                CheckHeldLoadEffectsAndRestore();
                CheckDynamiteBeforeHeldRender();
                CheckAvailableDiamondMole();
            }
            catch (Exception exception)
            {
                Failures.Add(exception.ToString()); Report.AppendLine("ERROR " + exception);
            }
            Report.AppendLine(Failures.Count == 0
                ? "GOLD_MINER_MECHANICS_CHECKS_PASS: " + checks + " assertions"
                : "GOLD_MINER_MECHANICS_CHECKS_FAIL: " + Failures.Count + " of " + checks + " assertions failed");
            File.WriteAllText(Path.Combine(Output, "report.txt"), Report.ToString());
            Debug.Log(Report.ToString());
            EditorApplication.Exit(Failures.Count == 0 ? 0 : 1);
        }

        private static void CheckWholeMinerContract(Fixture fixture)
        {
            for (int player = 0; player < 2; player++)
                Check(fixture.OptionalPart("Whole miner frame P" + (player + 1)) != null,
                    "P" + (player + 1) + " miner, both arms, crank and winch use one complete illustrated frame");
            Check(!fixture.HasPartPrefix("Miner hand") && !fixture.HasPartPrefix("Miner upper arm") &&
                !fixture.HasPartPrefix("Miner resting hand") && !fixture.HasPartPrefix("Miner resting upper arm") &&
                !fixture.HasPartPrefix("Miner head") && !fixture.HasPartPrefix("Miner body") &&
                !fixture.HasPartPrefix("Winch crank") && !fixture.HasPartPrefix("Winch frame") &&
                !fixture.HasPartPrefix("Winch drum") && !fixture.HasPartPrefix("Winch wooden grip") &&
                !fixture.HasPartPrefix("Winding surface"),
                "Whole-frame animation removes the split arm/crank/drum rig instead of retaining hidden old components");
        }

        private static void CheckHooks(Fixture fixture)
        {
            fixture.Scene.Render(fixture.Run, 0); Canvas.ForceUpdateCanvases();
            for (int player = 0; player < 2; player++)
            {
                string label = "P" + (player + 1) + " empty hook";
                var hook = fixture.Part("Hook P" + (player + 1)).GetComponent<Image>();
                var rope = (RectTransform)fixture.Part("Rope P" + (player + 1));
                Check(hook.sprite == fixture.Sprite("characters/hook-open-closed", "open"), label + " uses authored open sprite");
                Check(Vector2.Distance(hook.rectTransform.pivot, GoldMinerCaughtArt.OpenRing) < .00001f,
                    label + " pivot is the measured suspension eye");
                float expectedHeight = Mathf.Clamp(fixture.Layout.Scale * .60f, 36, 64);
                Check(Mathf.Abs(hook.rectTransform.rect.height - expectedHeight) < .001f,
                    label + " size follows the approved world scale and 36-64 pixel bounds");
                CheckHookAxisAndAspect(hook, rope, fixture.Run.Current.Hooks[player].AngleRadians, label);
                var ropeEnd = rope.TransformPoint(new Vector3(0, -rope.rect.height));
                var ringCenter = ImagePoint(hook, GoldMinerCaughtArt.OpenRing);
                Check(Vector3.Distance(ropeEnd, ringCenter) < .02f,
                    label + " rope terminates at the visible suspension eye; gap=" + Vector3.Distance(ropeEnd, ringCenter));
            }
        }

        private static void CheckHeldObjectGrip()
        {
            var openDiameters = new float[2];
            using (var empty = new Fixture())
                for (int player = 0; player < 2; player++)
                    openDiameters[player] = OpenRingDiameter(empty.Part("Hook P" + (player + 1)).GetComponent<Image>());
            foreach (GoldMinerKind kind in Enum.GetValues(typeof(GoldMinerKind)))
            using (var fixture = new Fixture(kind))
            {
                var spec = GoldMinerCaughtArt.For(kind);
                foreach (float angle in new[] { -.9f, 0, .9f })
                {
                    SetHeldAngle(fixture, angle);
                    string beforeModel = ModelText(fixture.Run);
                    fixture.Scene.Render(fixture.Run, 0); Canvas.ForceUpdateCanvases();
                    for (int player = 0; player < 2; player++)
                    {
                        string label = kind + " P" + (player + 1) + " angle " + angle;
                        var hook = fixture.Part("Hook P" + (player + 1)).GetComponent<Image>();
                        var ore = fixture.Part("Ore " + (player + 1)).GetComponent<Image>();
                        var rope = (RectTransform)fixture.Part("Rope P" + (player + 1));
                        Check(hook.sprite == fixture.Sprite(spec.Resource), label + " uses the complete authored claw/load sprite " + spec.Resource);
                        Check(!ore.gameObject.activeSelf && !ore.gameObject.activeInHierarchy,
                            label + " hides the independent ore image instead of layering it through the claw");
                        Check(Vector2.Distance(hook.rectTransform.pivot, spec.Ring) < .00001f,
                            label + " pivot is the measured eye in its own source image");
                        var ring = ImagePoint(hook, spec.Ring);
                        var ropeEnd = rope.TransformPoint(new Vector3(0, -rope.rect.height));
                        Check(Vector3.Distance(ropeEnd, ring) < .02f,
                            label + " actual illustrated suspension eye meets the rope endpoint; gap=" + Vector3.Distance(ropeEnd, ring));
                        float openDiameter = openDiameters[player];
                        float loadedDiameter = Vector3.Distance(
                            ImagePoint(hook, spec.Ring - Vector2.right * spec.RingDiameter * .5f),
                            ImagePoint(hook, spec.Ring + Vector2.right * spec.RingDiameter * .5f));
                        Check(Mathf.Abs(loadedDiameter - openDiameter) < .02f,
                            label + " loaded and empty hooks keep the same visible eye diameter; loaded=" + loadedDiameter + " empty=" + openDiameter);
                        CheckHookAxisAndAspect(hook, rope, angle, label);
                        var center = ImagePoint(hook, spec.LoadCenter);
                        Check(Vector3.Dot(center - ring, hook.transform.TransformDirection(Vector3.down).normalized) > 0,
                            label + " illustrated load hangs beyond the suspension eye");
                        var position = hook.rectTransform.localPosition;
                        var rotation = hook.rectTransform.localRotation;
                        var size = hook.rectTransform.sizeDelta; var sprite = hook.sprite;
                        fixture.Scene.Render(fixture.Run, 0);
                        Check(Vector3.Distance(position, hook.rectTransform.localPosition) < .001f &&
                            Quaternion.Angle(rotation, hook.rectTransform.localRotation) < .001f &&
                            Vector2.Distance(size, hook.rectTransform.sizeDelta) < .001f && sprite == hook.sprite,
                            label + " paused render preserves the entire illustrated load transform");
                    }
                    Check(ModelText(fixture.Run) == beforeModel, kind + " angle " + angle + " rendering and pause preserve every serialized model field");
                    Check(!fixture.HasPartPrefix("Carried diamond"), kind + " has no separate floating carried-diamond node");
                    var captureHook = fixture.Part("Hook P1").GetComponent<Image>();
                    fixture.Capture("grip-" + kind + "-" + (angle < 0 ? "left" : angle > 0 ? "right" : "center"),
                        ImagePoint(captureHook, spec.LoadCenter), 100, 600, 600);
                }
            }
        }

        private static void CheckHeldLoadEffectsAndRestore()
        {
            foreach (GoldMinerKind kind in Enum.GetValues(typeof(GoldMinerKind)))
            foreach (float angle in new[] { -.9f, 0, .9f })
            using (var fixture = new Fixture(kind))
            {
                SetHeldAngle(fixture, angle); fixture.Scene.Render(fixture.Run, 0); Canvas.ForceUpdateCanvases();
                var spec = GoldMinerCaughtArt.For(kind); string beforeModel = ModelText(fixture.Run);
                using (var restored = new Fixture(fixture.Run))
                {
                    Check(ModelText(restored.Run) == beforeModel, kind + " angle " + angle + " rebuilding preserves every snapshot field");
                    for (int player = 0; player < 2; player++)
                    {
                        string label = kind + " P" + (player + 1) + " angle " + angle;
                        var load = fixture.Part("Hook P" + (player + 1)).GetComponent<Image>();
                        var copy = restored.Part("Hook P" + (player + 1)).GetComponent<Image>();
                        Check(Vector3.Distance(load.rectTransform.localPosition, copy.rectTransform.localPosition) < .001f &&
                            Quaternion.Angle(load.transform.localRotation, copy.transform.localRotation) < .001f &&
                            Vector2.Distance(load.rectTransform.sizeDelta, copy.rectTransform.sizeDelta) < .001f &&
                            Vector2.Distance(load.rectTransform.pivot, copy.rectTransform.pivot) < .00001f && load.sprite == copy.sprite,
                            label + " rebuilding restores the complete claw/load transform without animation history");
                        Check(!restored.Part("Ore " + (player + 1)).gameObject.activeSelf,
                            label + " rebuilding does not resurrect the hidden independent ore");
                        fixture.Scene.Explode(fixture.Run, player);
                        var explosion = fixture.NewestPart("fx_explosion").GetComponent<Image>();
                        Check(Vector3.Distance(ImagePoint(explosion, Vector2.one * .5f), ImagePoint(load, spec.LoadCenter)) < .02f,
                            label + " dynamite burst follows the illustrated load center rather than its eye or canvas center");
                    }
                }
                Check(ModelText(fixture.Run) == beforeModel, kind + " angle " + angle + " effects do not mutate the model");
            }
        }

        private static void CheckDynamiteBeforeHeldRender()
        {
            // Input synchronization may catch/retrieve a load after the last Render but
            // before the bomb command. Exercise both an uninitialized and a stale view.
            foreach (GoldMinerKind kind in Enum.GetValues(typeof(GoldMinerKind)))
            using (var fixture = new Fixture(kind, initiallyHeld: false))
            {
                var beforeCommand = fixture.Run.Copy();
                for (int player = 0; player < 2; player++)
                {
                    var hook = beforeCommand.Current.Hooks[player];
                    hook.HeldEntityId = player + 1; hook.State = GoldMinerHookState.RetractingLoaded;
                    hook.AngleRadians = player == 0 ? -.7 : .8; hook.Length = player == 0 ? 2.1 : 4.2;
                    var entity = beforeCommand.Current.Entities[player];
                    entity.Status = GoldMinerEntityStatus.Held; entity.OwnerPlayerId = player;
                    entity.X = GoldMinerRules.OriginX(beforeCommand.Mode, player) + Math.Sin(hook.AngleRadians) * hook.Length;
                    entity.Y = Math.Cos(hook.AngleRadians) * hook.Length;
                }
                string captured = ModelText(beforeCommand);
                for (int player = 0; player < 2; player++)
                {
                    fixture.Scene.Explode(beforeCommand, player);
                    var explosion = fixture.NewestPart("fx_explosion").GetComponent<Image>();
                    var expected = SnapshotLoadCenter(fixture, beforeCommand, player);
                    Check(Vector3.Distance(ImagePoint(explosion, Vector2.one * .5f), expected) < .02f,
                        kind + " P" + (player + 1) + " freshly caught bomb uses the current snapshot before any held Render");
                    Check(fixture.Part("Hook P" + (player + 1)).GetComponent<Image>().sprite ==
                        fixture.Sprite("characters/hook-open-closed", "open"),
                        kind + " P" + (player + 1) + " fresh-catch regression really starts with the old empty-hook view");
                }
                Check(ModelText(beforeCommand) == captured, kind + " fresh-catch effect preserves the pre-command snapshot");

                fixture.Scene.Render(beforeCommand, 0);
                var moved = beforeCommand.Copy();
                for (int player = 0; player < 2; player++)
                {
                    var hook = moved.Current.Hooks[player]; hook.Length -= .4;
                    var entity = moved.Current.Entities[player];
                    entity.X = GoldMinerRules.OriginX(moved.Mode, player) + Math.Sin(hook.AngleRadians) * hook.Length;
                    entity.Y = Math.Cos(hook.AngleRadians) * hook.Length;
                }
                string movedModel = ModelText(moved);
                for (int player = 0; player < 2; player++)
                {
                    var oldCenter = ImagePoint(fixture.Part("Hook P" + (player + 1)).GetComponent<Image>(), GoldMinerCaughtArt.For(kind).LoadCenter);
                    var expected = SnapshotLoadCenter(fixture, moved, player);
                    Check(Vector3.Distance(oldCenter, expected) > 1,
                        kind + " P" + (player + 1) + " stale-render regression has a visibly different current load location");
                    fixture.Scene.Explode(moved, player);
                    var explosion = fixture.NewestPart("fx_explosion").GetComponent<Image>();
                    Check(Vector3.Distance(ImagePoint(explosion, Vector2.one * .5f), expected) < .02f,
                        kind + " P" + (player + 1) + " bomb follows current retrieval instead of the previous Render center");
                }
                Check(ModelText(moved) == movedModel, kind + " stale-view effect preserves the current pre-command snapshot");
            }
        }

        private static Vector3 SnapshotLoadCenter(Fixture fixture, GoldMinerRunSnapshot run, int player)
        {
            var hook = run.Current.Hooks[player]; GoldMinerEntity held = null;
            foreach (var entity in run.Current.Entities)
                if (entity.Id == hook.HeldEntityId) { held = entity; break; }
            if (held == null) throw new InvalidOperationException("Missing held regression entity");
            var spec = GoldMinerCaughtArt.For(held.Kind);
            float openHeight = Mathf.Clamp(fixture.Layout.Scale * .60f, 36, 64);
            float width = openHeight * GoldMinerCaughtArt.OpenRingDiameterPerHeight / spec.RingDiameter;
            var sprite = fixture.Sprite(spec.Resource);
            var size = new Vector2(width, width * sprite.rect.height / sprite.rect.width);
            var localOffset = Vector2.Scale(spec.LoadCenter - spec.Ring, size);
            var rotatedOffset = Quaternion.AngleAxis((float)(hook.AngleRadians * 180 / Math.PI), Vector3.forward) * (Vector3)localOffset;
            var end = fixture.Layout.World(GoldMinerRules.OriginX(run.Mode, player) + Math.Sin(hook.AngleRadians) * hook.Length,
                Math.Cos(hook.AngleRadians) * hook.Length);
            return fixture.Root.TransformPoint(new Vector3(fixture.Root.rect.xMin + end.x + rotatedOffset.x,
                fixture.Root.rect.yMax - end.y + rotatedOffset.y));
        }

        private static void CheckAvailableDiamondMole()
        {
            using (var fixture = new Fixture(GoldMinerKind.DiamondMole))
            {
                for (int player = 0; player < 2; player++)
                {
                    var entity = fixture.Run.Current.Entities[player];
                    entity.Status = GoldMinerEntityStatus.Available; entity.OwnerPlayerId = null;
                    fixture.Run.Current.Hooks[player].HeldEntityId = null;
                    fixture.Run.Current.Hooks[player].State = GoldMinerHookState.Swinging;
                }
                string beforeModel = ModelText(fixture.Run);
                fixture.Scene.Render(fixture.Run, .125f); Canvas.ForceUpdateCanvases();
                Check(!fixture.HasPartPrefix("Carried diamond"), "Available diamond moles use no independently placed diamond node");
                for (int player = 0; player < 2; player++)
                {
                    var ore = fixture.Part("Ore " + (player + 1)).GetComponent<Image>();
                    Check(ore.gameObject.activeInHierarchy && ore.sprite == fixture.Sprite("characters/mole-carrying-diamond-run"),
                        "Available diamond mole " + (player + 1) + " displays the complete body/paws/gem illustration");
                    Check(Mathf.Sign(ore.rectTransform.localScale.x) == fixture.Run.Current.Entities[player].MoveDirection,
                        "Available diamond mole " + (player + 1) + " mirrors its whole illustration with travel direction");
                    var position = ore.rectTransform.localPosition; var rotation = ore.rectTransform.localRotation;
                    fixture.Scene.Render(fixture.Run, 0);
                    Check(Vector3.Distance(position, ore.rectTransform.localPosition) < .001f &&
                        Quaternion.Angle(rotation, ore.rectTransform.localRotation) < .001f,
                        "Available diamond mole " + (player + 1) + " pauses its complete illustration without gem drift");
                    var hook = fixture.Part("Hook P" + (player + 1)).GetComponent<Image>();
                    Check(hook.sprite == fixture.Sprite("characters/hook-open-closed", "open") &&
                        Vector2.Distance(hook.rectTransform.pivot, GoldMinerCaughtArt.OpenRing) < .00001f,
                        "Released diamond mole " + (player + 1) + " restores the measured open-hook pivot");
                }
                Check(ModelText(fixture.Run) == beforeModel, "Available diamond-mole gait never changes collision/save coordinates");
            }
        }

        private static void SetHeldAngle(Fixture fixture, float angle)
        {
            for (int player = 0; player < 2; player++)
            {
                var hook = fixture.Run.Current.Hooks[player]; hook.AngleRadians = angle;
                var entity = fixture.Run.Current.Entities[player];
                entity.X = GoldMinerRules.OriginX(fixture.Run.Mode, player) + Math.Sin(angle) * hook.Length;
                entity.Y = Math.Cos(angle) * hook.Length;
            }
        }
        private static string ModelText(GoldMinerRunSnapshot run) => Newtonsoft.Json.JsonConvert.SerializeObject(run);
        private static float OpenRingDiameter(Image image)
        {
            var half = Vector2.up * GoldMinerCaughtArt.OpenRingDiameterPerHeight * .5f;
            return Vector3.Distance(ImagePoint(image, GoldMinerCaughtArt.OpenRing - half), ImagePoint(image, GoldMinerCaughtArt.OpenRing + half));
        }
        private static void CheckHookAxisAndAspect(Image hook, RectTransform rope, double angle, string label)
        {
            var expected = new Vector3((float)Math.Sin(angle), -(float)Math.Cos(angle), 0);
            Check(Vector3.Dot(hook.transform.TransformDirection(Vector3.down).normalized, expected) > .9999f &&
                Quaternion.Angle(hook.rectTransform.localRotation, rope.localRotation) < .001f,
                label + " complete hook/load axis follows the rope angle");
            float aspect = hook.rectTransform.rect.width / hook.rectTransform.rect.height;
            float spriteAspect = hook.sprite.rect.width / hook.sprite.rect.height;
            Check(Mathf.Abs(aspect - spriteAspect) < .001f, label + " preserves complete authored image aspect; rect=" + aspect + " sprite=" + spriteAspect);
        }

        private static void CheckWinches(Fixture fixture)
        {
            CheckReelClock();
            var first = fixture.Part("Whole miner frame P1").GetComponent<Image>();
            var second = fixture.Part("Whole miner frame P2").GetComponent<Image>();
            var firstPosition = first.rectTransform.anchoredPosition; var firstSize = first.rectTransform.sizeDelta;
            var secondSprite = second.sprite;
            Check(first.sprite.name == "frame_0" && second.sprite.name == "frame_0", "Both newly built miners begin on their actual first illustrated frame");
            fixture.Run.Current.Hooks[0].State = GoldMinerHookState.RetractingEmpty;
            fixture.Run.Current.Hooks[0].Length -= .505;
            fixture.Scene.Render(fixture.Run, .101f);
            Check(first.sprite.name == "frame_1", "P1 switches to the next complete artwork after real light-load retrieval");
            Check(second.sprite == secondSprite, "P1 retrieval does not advance P2 artwork");
            Check(Vector2.Distance(firstPosition, first.rectTransform.anchoredPosition) < .001f &&
                Vector2.Distance(firstSize, first.rectTransform.sizeDelta) < .001f,
                "Changing whole frames does not change the fixed display size or suspension position");
            var firstSprite = first.sprite;
            fixture.Scene.Render(fixture.Run, .75f); fixture.Scene.Render(fixture.Run, 0); fixture.Scene.Render(fixture.Run, -.25f);
            Check(first.sprite == firstSprite && second.sprite == secondSprite, "Stationary, paused and negative-delta renders preserve both displayed frames");
            fixture.Run.Current.Hooks[1].State = GoldMinerHookState.RetractingEmpty;
            fixture.Run.Current.Hooks[1].Length -= .505;
            fixture.Scene.Render(fixture.Run, .101f);
            Check(second.sprite.name == "frame_1" && first.sprite == firstSprite, "P2 retrieval advances only P2's independent complete-frame loop");
            CheckWholeAnchors(fixture, 0, "independent retrieval"); CheckWholeAnchors(fixture, 1, "independent retrieval");
            using (var rebuilt = new Fixture(fixture.Run))
            {
                Check(rebuilt.Part("Whole miner frame P1").GetComponent<Image>().sprite.name == "frame_0" &&
                    rebuilt.Part("Whole miner frame P2").GetComponent<Image>().sprite.name == "frame_0",
                    "A rebuilt view starts a fresh display loop without inheriting another view's animation history");
                CheckWholeAnchors(rebuilt, 0, "rebuilt view"); CheckWholeAnchors(rebuilt, 1, "rebuilt view");
            }
        }

        private static void CheckReelClock()
        {
            var clock = new GoldMinerReelPlayback();
            var hook = new GoldMinerHook { State = GoldMinerHookState.RetractingEmpty, Length = 5 };
            clock.Observe(hook, GoldMinerPhase.Running, .5f, 5);
            Check(clock.FrameIndex == 0, "First render only establishes line length and cannot manufacture frame progress");
            hook.Length -= .505; clock.Observe(hook, GoldMinerPhase.Running, .101f, 5);
            Check(clock.FrameIndex == 1, "Positive actual retrieval enters the next reel frame");
            int still = clock.FrameIndex;
            clock.Observe(hook, GoldMinerPhase.Running, 1, 5);
            Check(clock.FrameIndex == still, "Elapsed render time without shortening never plays a frame");
            hook.Length -= .5; clock.Observe(hook, GoldMinerPhase.Running, 0, 5);
            hook.Length -= .5; clock.Observe(hook, GoldMinerPhase.Running, -.1f, 5);
            clock.Observe(hook, GoldMinerPhase.Running, .1f, 5);
            Check(clock.FrameIndex == still, "Paused and negative-delta position changes are not replayed as motion when rendering resumes");
            hook.State = GoldMinerHookState.Extending; hook.Length += .5;
            clock.Observe(hook, GoldMinerPhase.Running, .1f, 5);
            Check(clock.FrameIndex == still, "An extending line does not crank the reeling animation backwards");
            hook.State = GoldMinerHookState.RetractingEmpty; hook.Length -= .5;
            clock.Observe(hook, GoldMinerPhase.Won, .1f, 5);
            Check(clock.FrameIndex == still, "Terminal phases cannot advance a reeling loop");
            hook.Length -= .5; clock.Observe(hook, GoldMinerPhase.Running, float.NaN, 5);
            hook.Length -= .5; clock.Observe(hook, GoldMinerPhase.Running, .1f, double.PositiveInfinity);
            Check(clock.FrameIndex == still, "Nonfinite render delta or retrieval speed cannot poison animation phase");
            foreach (int fps in new[] { 30, 60, 120 })
            {
                double light = ReelCycleSeconds(5, fps), heavy = ReelCycleSeconds(.48, fps);
                Check(light >= .5 && light <= .7, fps + " Hz light retrieval completes one real six-frame cycle in " + light + " seconds");
                Check(heavy >= .8 && heavy <= 1.0, fps + " Hz heavy retrieval completes one real six-frame cycle in " + heavy + " seconds");
                Check(heavy - light >= .2, fps + " Hz heavy and light loads visibly differ without freezing slow loads");
            }
        }

        private static double ReelCycleSeconds(double speed, int renderFps)
        {
            var clock = new GoldMinerReelPlayback();
            var hook = new GoldMinerHook { State = GoldMinerHookState.RetractingLoaded, Length = 7 };
            clock.Observe(hook, GoldMinerPhase.Running, 0, speed);
            int previous = 0; var frames = new HashSet<int> { 0 };
            for (int rendered = 1; rendered <= renderFps * 2; rendered++)
            {
                // Core advances at 60 Hz. A 120 Hz display sees unchanged length every other frame.
                int ticks = (rendered * 60 / renderFps) - ((rendered - 1) * 60 / renderFps);
                hook.Length -= ticks * speed / 60;
                clock.Observe(hook, GoldMinerPhase.Running, 1f / renderFps, speed);
                frames.Add(clock.FrameIndex);
                if (clock.FrameIndex < previous)
                {
                    Check(frames.Count == 6, renderFps + " Hz loop displays all six distinct frame indices before wrapping");
                    return rendered / (double)renderFps;
                }
                previous = clock.FrameIndex;
            }
            Check(false, "Reel animation did not complete its bounded two-second cycle");
            return 2;
        }

        private static void CheckWholeAnchors(Fixture fixture, int player, string context)
        {
            var image = fixture.Part("Whole miner frame P" + (player + 1)).GetComponent<Image>();
            var lead = (RectTransform)fixture.Part("Winch rope lead P" + (player + 1));
            var rope = (RectTransform)fixture.Part("Rope P" + (player + 1));
            var pivot = new Vector2(image.sprite.pivot.x / image.sprite.rect.width, image.sprite.pivot.y / image.sprite.rect.height);
            Check(Vector2.Distance(image.rectTransform.pivot, pivot) < .00001f,
                "P" + (player + 1) + " uses the imported source-measured outlet pivot " + context);
            Check(Vector3.Distance(ImagePoint(image, pivot), lead.TransformPoint(new Vector3(0, lead.rect.yMax))) < .02f,
                "P" + (player + 1) + " authored short-rope endpoint joins the hemp lead " + context);
            Check(Vector3.Distance(lead.TransformPoint(new Vector3(0, lead.rect.yMin)), rope.TransformPoint(new Vector3(0, rope.rect.yMax))) < .02f,
                "P" + (player + 1) + " hemp lead joins the fixed main-rope origin " + context);
            Check(Quaternion.Angle(image.rectTransform.localRotation, Quaternion.identity) < .001f,
                "P" + (player + 1) + " complete miner/machine image never spins or bobs " + context);
            Check(Mathf.Sign(image.transform.parent.localScale.x) == (player == 0 ? 1 : -1),
                "P" + (player + 1) + " mirrors only the complete authored assembly in its intended direction " + context);
        }

        private static void CheckRope(Fixture fixture)
        {
            Canvas.ForceUpdateCanvases();
            for (int player = 0; player < 2; player++)
            {
                var rope = fixture.Part("Rope P" + (player + 1)).GetComponent<Graphic>();
                var lead = fixture.Part("Winch rope lead P" + (player + 1)).GetComponent<Graphic>();
                Check(rope.GetType() == lead.GetType() && rope.GetType().Name == "GoldMinerHempRope",
                    "P" + (player + 1) + " hanging rope and winch lead share hemp rendering");
                using (var mesh = Populate(rope))
                {
                    var colors = new HashSet<Color32>(); var bounds = rope.rectTransform.rect; bool contained = true;
                    for (int i = 0; i < mesh.currentVertCount; i++)
                    {
                        var vertex = new UIVertex(); mesh.PopulateUIVertex(ref vertex, i); colors.Add(vertex.color);
                        contained &= vertex.position.x >= bounds.xMin - .001f && vertex.position.x <= bounds.xMax + .001f &&
                            vertex.position.y >= bounds.yMin - .001f && vertex.position.y <= bounds.yMax + .001f;
                    }
                    Check(mesh.currentVertCount > 24 && colors.Count >= 4,
                        "P" + (player + 1) + " rope has shaded winding strands; vertices=" + mesh.currentVertCount + " colors=" + colors.Count);
                    Check(contained, "P" + (player + 1) + " rope weave stays within line endpoints");
                }
                // A working mesh generator alone does not draw anything. Graphic
                // requires only RectTransform; these custom graphics must also
                // provide CanvasRenderer and upload their geometry/material to it.
                foreach (string name in new[] { "Rope P", "Winch rope lead P" })
                {
                    var part = fixture.Part(name + (player + 1));
                    var renderer = part.GetComponent<CanvasRenderer>();
                    Check(renderer != null, part.name + " has an actual CanvasRenderer");
                    if (renderer == null) continue;
                    var renderedMesh = renderer.GetMesh();
                    Check(renderedMesh != null && renderedMesh.vertexCount > 0 && renderer.materialCount > 0,
                        part.name + " uploads visible mesh and material to CanvasRenderer; vertices=" +
                        (renderedMesh == null ? 0 : renderedMesh.vertexCount) + " materials=" + renderer.materialCount);
                }
            }
        }

        private static void CapturePhases(Fixture fixture)
        {
            foreach (var hook in fixture.Run.Current.Hooks) hook.State = GoldMinerHookState.RetractingEmpty;
            for (int frame = 0; frame < 6; frame++)
            {
                if (frame > 0)
                {
                    foreach (var hook in fixture.Run.Current.Hooks) hook.Length -= .505;
                    fixture.Scene.Render(fixture.Run, .101f);
                }
                for (int player = 0; player < fixture.Run.Current.Hooks.Length; player++)
                {
                    var image = fixture.Part("Whole miner frame P" + (player + 1)).GetComponent<Image>();
                    Check(image.sprite == fixture.Sprite("characters/miner-p" + (player + 1) + "-reel-v4", "frame_" + frame),
                        "Actual " + fixture.Run.Mode + " P" + (player + 1) + " renders its own authored frame " + frame);
                    CheckWholeAnchors(fixture, player, "frame " + frame);
                    fixture.Capture("miner-" + fixture.Run.Mode.ToString().ToLowerInvariant() + "-p" + (player + 1) + "-frame-" + frame,
                        ImagePoint(image, Vector2.one * .5f), image.rectTransform.rect.height * .7f, 768, 768);
                }
                CheckHeadClearance(fixture);
                if (frame == 0 && fixture.Run.Mode == GoldMinerMode.Coop)
                    fixture.Capture("mechanics-coop-overview", Vector3.zero, 360, 1320, 720);
            }
        }

        private static void CheckRopeTravel(Fixture fixture)
        {
            const float pitch = 4.4f * 1.75f;
            float beforePixels = 3.5f * fixture.Layout.Scale;
            float afterPixels = 3.49f * fixture.Layout.Scale;
            float expectedTravel = beforePixels - afterPixels;
            Check(expectedTravel > .1f && expectedTravel < pitch * .5f,
                "Weave regression uses a sub-pitch displacement, not an invisible complete repeat; expected=" + expectedTravel);
            // Historical loop started at -length-pitch+Repeat(length,pitch).
            // Modulo pitch that is constant: this explicit counterexample proves
            // the visual-motion assertion below would reject the old equation.
            float legacyBefore = Mathf.Repeat(-beforePixels - pitch + Mathf.Repeat(beforePixels, pitch), pitch);
            float legacyAfter = Mathf.Repeat(-afterPixels - pitch + Mathf.Repeat(afterPixels, pitch), pitch);
            float legacyTravel = PhaseDelta(legacyBefore, legacyAfter, pitch);
            Check(Mathf.Abs(legacyTravel) < .001f && Mathf.Abs(legacyTravel - expectedTravel) > .1f,
                "Historical yMin-based weave cancels real rope travel and fails required visual displacement; old=" + legacyTravel);

            foreach (var hook in fixture.Run.Current.Hooks)
            { hook.Length = 3.5; hook.State = GoldMinerHookState.RetractingEmpty; }
            fixture.Scene.Render(fixture.Run, 0); Canvas.ForceUpdateCanvases();
            for (int player = 0; player < 2; player++)
            {
                var rope = fixture.Part("Rope P" + (player + 1)).GetComponent<Graphic>();
                var lead = fixture.Part("Winch rope lead P" + (player + 1)).GetComponent<Graphic>();
                var other = fixture.Part("Rope P" + (2 - player)).GetComponent<Graphic>();
                float ropeBefore = TopWeavePhase(rope), leadBefore = TopWeavePhase(lead), otherBefore = TopWeavePhase(other);
                fixture.Run.Current.Hooks[player].Length = 3.49;
                fixture.Scene.Render(fixture.Run, .25f); Canvas.ForceUpdateCanvases();
                float ropeAfter = TopWeavePhase(rope), leadAfter = TopWeavePhase(lead);
                float ropeTravel = PhaseDelta(ropeBefore, ropeAfter, pitch);
                float leadTravel = PhaseDelta(leadBefore, leadAfter, pitch);
                Check(Mathf.Abs(ropeTravel - expectedTravel) < .003f,
                    "P" + (player + 1) + " actual upper rope weave moves upward with 3.5 to 3.49 retrieval; travel=" + ropeTravel);
                Check(Mathf.Abs(leadTravel - expectedTravel) < .003f,
                    "P" + (player + 1) + " actual winch-lead weave follows same physical travel after rig scaling; travel=" + leadTravel);
                Check(Mathf.Abs(ropeTravel - leadTravel) < .003f,
                    "P" + (player + 1) + " hanging and lead rope have matching world-space weave speeds");
                Check(Mathf.Abs(PhaseDelta(otherBefore, TopWeavePhase(other), pitch)) < .001f,
                    "P" + (player + 1) + " retrieval does not move the other player's rope weave");
                fixture.Scene.Render(fixture.Run, .75f); fixture.Scene.Render(fixture.Run, 0); Canvas.ForceUpdateCanvases();
                Check(Mathf.Abs(PhaseDelta(ropeAfter, TopWeavePhase(rope), pitch)) < .001f,
                    "P" + (player + 1) + " paused/unchanged rope length freezes actual hanging weave geometry");
                Check(Mathf.Abs(PhaseDelta(leadAfter, TopWeavePhase(lead), pitch)) < .001f,
                    "P" + (player + 1) + " paused/unchanged rope length freezes actual lead weave geometry");
            }
        }

        private static float TopWeavePhase(Graphic rope)
        {
            // Inspect uploaded geometry in a fixed interval below the attachment,
            // independent of the moving lower endpoint and changing vertex count.
            var mesh = rope.canvasRenderer.GetMesh();
            var vertices = mesh.vertices; var colors = mesh.colors32;
            float unitScale = rope.rectTransform.TransformVector(Vector3.up).magnitude;
            float pitch = rope.rectTransform.rect.width * 1.75f * unitScale;
            var shade = new Color32(161, 120, 64, 255);
            for (int i = 0; i < vertices.Length; i += 4)
            {
                float y = (vertices[i].y - rope.rectTransform.rect.yMax) * unitScale;
                float length = rope.rectTransform.rect.height * unitScale;
                bool inFixedInterval = length >= 40 ? y < -10 && y > -30 : y < -.0001f && y > -length + .0001f;
                if (colors[i].Equals(shade) && inFixedInterval) return Mathf.Repeat(y, pitch);
            }
            throw new InvalidOperationException(rope.name + " has no uploaded interior winding vertex in fixed upper interval");
        }

        private static float PhaseDelta(float before, float after, float pitch)
        {
            return Mathf.Repeat(after - before + pitch * .5f, pitch) - pitch * .5f;
        }

        private static void CheckHeadClearance(Fixture fixture)
        {
            Canvas.ForceUpdateCanvases();
            float hudBottom = fixture.Root.TransformPoint(new Vector3(0, fixture.Root.rect.yMax - fixture.Layout.Hud.yMax)).y;
            for (int player = 0; player < fixture.Run.Current.Hooks.Length; player++)
            {
                var image = fixture.Part("Whole miner frame P" + (player + 1)).GetComponent<Image>();
                var mesh = image.canvasRenderer.GetMesh();
                Check(mesh != null && mesh.vertexCount > 0, "P" + (player + 1) + " whole-frame clearance uses uploaded artwork geometry");
                if (mesh == null || mesh.vertexCount == 0) continue;
                float top = float.NegativeInfinity;
                foreach (var vertex in mesh.vertices) top = Mathf.Max(top, image.transform.TransformPoint(vertex).y);
                Check(top <= hudBottom - 7.5f,
                    "P" + (player + 1) + " complete frame including transparent margins clears the HUD; gap=" + (hudBottom - top));
            }
        }

        private static Vector3 ImagePoint(Image image, Vector2 normalized)
        {
            var rect = image.rectTransform.rect;
            return image.rectTransform.TransformPoint(new Vector3(rect.xMin + rect.width * normalized.x, rect.yMin + rect.height * normalized.y));
        }
        private static VertexHelper Populate(Graphic graphic)
        {
            var method = graphic.GetType().GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(VertexHelper) }, null);
            if (method == null) throw new InvalidOperationException("Missing mesh generator for " + graphic.name);
            var mesh = new VertexHelper(); method.Invoke(graphic, new object[] { mesh }); return mesh;
        }
        private static void Check(bool pass, string description)
        {
            checks++; Report.AppendLine((pass ? "PASS " : "FAIL ") + description);
            if (!pass) Failures.Add(description);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject canvasObject;
            private readonly GoldMinerArtwork art;
            private readonly Camera camera;
            private readonly RectTransform root;
            public RectTransform Root => root;
            public readonly XanaduYonder.Runtime.GoldMiner.GoldMinerLayout Layout;
            public readonly GoldMinerRunSnapshot Run;
            public readonly GoldMinerSceneView Scene;

            public Fixture(GoldMinerRunSnapshot snapshot) : this(null, snapshot) { }

            public Fixture(GoldMinerKind? heldKind = null, GoldMinerRunSnapshot snapshot = null, bool initiallyHeld = true, GoldMinerMode mode = GoldMinerMode.Coop)
            {
                mode = snapshot == null ? mode : snapshot.Mode;
                float width = mode == GoldMinerMode.Coop ? 1320 : 720, height = mode == GoldMinerMode.Coop ? 720 : 1320;
                canvasObject = new GameObject("Gold Miner mechanics fixture", typeof(RectTransform), typeof(Canvas));
                canvasObject.hideFlags = HideFlags.DontSave;
                var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                root = canvasObject.GetComponent<RectTransform>(); root.sizeDelta = new Vector2(width, height);
                camera = new GameObject("Gold Miner mechanics fixture camera", typeof(Camera)).GetComponent<Camera>();
                camera.gameObject.hideFlags = HideFlags.DontSave;
                camera.enabled = false; camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 1000;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.93f, .84f, .64f);
                camera.cullingMask = 1 << 29; canvas.worldCamera = camera;
                art = new GoldMinerArtwork();
                Layout = new XanaduYonder.Runtime.GoldMiner.GoldMinerLayout(width, height, new Rect(0, 0, width, height), mode);
                Run = snapshot == null ? new GoldMinerRunSnapshot
                {
                    Mode = mode,
                    Current = new GoldMinerState
                    {
                        Phase = GoldMinerPhase.Running, Entities = new GoldMinerEntity[0],
                        Players = new GoldMinerPlayer[GoldMinerRules.PlayerCount(mode)],
                        Hooks = new GoldMinerHook[GoldMinerRules.PlayerCount(mode)],
                        ActiveBuffs = new GoldMinerBuffs(), PendingBuffs = new GoldMinerBuffs()
                    }
                } : snapshot.Copy();
                if (snapshot == null)
                    for (int player = 0; player < Run.Current.Hooks.Length; player++)
                    {
                        Run.Current.Players[player] = new GoldMinerPlayer { PlayerId = player };
                        Run.Current.Hooks[player] = new GoldMinerHook { PlayerId = player, State = GoldMinerHookState.Swinging,
                            Length = 3.5, AngleRadians = player == 0 ? .65 : -.65 };
                    }
                if (heldKind.HasValue)
                {
                    var held = new GoldMinerEntity[Run.Current.Hooks.Length];
                    for (int player = 0; player < held.Length; player++)
                    {
                        var hook = Run.Current.Hooks[player];
                        hook.HeldEntityId = initiallyHeld ? player + 1 : (int?)null;
                        hook.State = initiallyHeld ? GoldMinerHookState.RetractingLoaded : GoldMinerHookState.Swinging;
                        held[player] = new GoldMinerEntity { Id = player + 1, Kind = heldKind.Value,
                            Status = initiallyHeld ? GoldMinerEntityStatus.Held : GoldMinerEntityStatus.Available,
                            OwnerPlayerId = initiallyHeld ? player : (int?)null,
                            X = GoldMinerRules.OriginX(Run.Mode, player) + Math.Sin(hook.AngleRadians) * hook.Length,
                            Y = Math.Cos(hook.AngleRadians) * hook.Length, MoveDirection = player == 0 ? 1 : -1 };
                    }
                    Run.Current.Entities = held;
                }
                Scene = new GoldMinerSceneView(root, art, Layout, mode); Scene.Build(Run);
                foreach (var part in root.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 29;
                Canvas.ForceUpdateCanvases();
            }
            public Transform Part(string name)
            {
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
                throw new InvalidOperationException("Expected mechanics render part: " + name);
            }
            public Transform OptionalPart(string name)
            {
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
                return null;
            }
            public Sprite Sprite(string resource, string slice = null) => art.Get(resource, slice);
            public bool HasPartPrefix(string prefix)
            {
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name.StartsWith(prefix, StringComparison.Ordinal)) return true;
                return false;
            }
            public Transform NewestPart(string name)
            {
                Transform found = null;
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) found = child;
                if (found == null) throw new InvalidOperationException("Expected mechanics render part: " + name);
                return found;
            }
            public void Capture(string name, Vector3 center, float halfHeight, int width, int height)
            {
                var texture = new RenderTexture(width, height, 24); texture.Create();
                var image = new Texture2D(width, height, TextureFormat.RGB24, false); var oldActive = RenderTexture.active;
                try
                {
                    camera.transform.position = new Vector3(center.x, center.y, -100); camera.transform.rotation = Quaternion.identity;
                    camera.orthographicSize = halfHeight; camera.aspect = width / (float)height; camera.targetTexture = texture;
                    Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = texture;
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG()); Report.AppendLine("CAPTURE " + name + ".png");
                }
                finally
                {
                    camera.targetTexture = null; RenderTexture.active = oldActive;
                    Object.DestroyImmediate(image); texture.Release(); Object.DestroyImmediate(texture);
                }
            }
            public void Dispose()
            {
                Object.DestroyImmediate(canvasObject); Object.DestroyImmediate(camera.gameObject); art.Dispose();
            }
        }
    }
}
