using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Feeds the installed UGUI input module. No click/drag handlers are called by the test.
    internal sealed class LobbyInputTestDriver : IDisposable
    {
        private readonly StandaloneInputModule module;
        private readonly LobbyTestInput input;
        private readonly BaseInput previousInput;
        private readonly bool ownedModule, previousForce;
        private readonly CursorLockMode previousLock;

        internal LobbyInputTestDriver(EventSystem events)
        {
            module = events.GetComponent<StandaloneInputModule>();
            ownedModule = module == null;
            if (ownedModule) module = events.gameObject.AddComponent<StandaloneInputModule>();
            previousInput = module.inputOverride; previousForce = module.forceModuleActive;
            previousLock = Cursor.lockState; Cursor.lockState = CursorLockMode.None;
            input = events.gameObject.AddComponent<LobbyTestInput>();
            module.inputOverride = input; module.forceModuleActive = true;
            events.UpdateModules(); module.ActivateModule();
        }

        internal void Frame(Vector2 point, bool held, bool down = false, bool up = false)
        {
            input.Position = point; input.Held = held; input.Down = down; input.Up = up;
            module.UpdateModule(); module.Process();
            input.Down = input.Up = false;
        }

        internal void Tap(Vector2 point, Vector2 jitter = default(Vector2))
        {
            Frame(point, false);
            Frame(point, true, down: true);
            if (jitter != Vector2.zero) Frame(point + jitter, true);
            Frame(point + jitter, false, up: true);
        }

        public void Dispose()
        {
            module.DeactivateModule();
            module.inputOverride = previousInput; module.forceModuleActive = previousForce;
            Object.DestroyImmediate(input);
            if (ownedModule) Object.DestroyImmediate(module);
            Cursor.lockState = previousLock;
        }

        internal static void CheckGestures(EventSystem events, Camera camera, ScrollRect scroll, Button card,
            Action flush, Action<bool, string> require)
        {
            var original = card.onClick;
            int clicks = 0;
            card.onClick = new Button.ButtonClickedEvent(); card.onClick.AddListener(() => clicks++);
            var rect = (RectTransform)card.transform;
            var initialPosition = scroll.content.anchoredPosition;
            try
            {
                using (var driver = new LobbyInputTestDriver(events))
                {
                    foreach (var sample in new[] { new Vector2(.15f, .7f), new Vector2(.8f, .65f), new Vector2(.45f, .15f) })
                    {
                        flush();
                        var local = new Vector2(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, sample.x),
                            Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, sample.y));
                        int before = clicks;
                        driver.Tap(RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(local)));
                        require(clicks == before + 1, card.name + " title, artwork and whitespace accept one module-driven tap");
                    }
                    var center = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
                    int taps = clicks;
                    driver.Tap(center, new Vector2(events.pixelDragThreshold * .4f, 0));
                    require(clicks == taps + 1, card.name + " sub-threshold jitter remains a single tap");
                    taps = clicks;
                    driver.Frame(center, true, down: true);
                    driver.Frame(center + Vector2.up * (events.pixelDragThreshold + 8), true);
                    driver.Frame(center + Vector2.up * 140, true);
                    require(Mathf.Abs(scroll.content.anchoredPosition.y - initialPosition.y) > 20,
                        card.name + " drag starts on the card and moves the actual ScrollRect");
                    driver.Frame(center + Vector2.up * 140, false, up: true);
                    require(clicks == taps, card.name + " scrolling never invokes its entry callback on release");
                    scroll.StopMovement(); scroll.content.anchoredPosition = initialPosition; flush();
                    driver.Frame(center, true, down: true);
                    driver.Frame(center + Vector2.up * (events.pixelDragThreshold + 8), true);
                    driver.Frame(center + Vector2.up * 100, true);
                    driver.Frame(center, true);
                    driver.Frame(center, false, up: true);
                    require(clicks == taps, card.name + " returning to the press point cannot revive a cancelled click");
                    scroll.StopMovement(); scroll.content.anchoredPosition = initialPosition; flush();
                    driver.Tap(center);
                    require(clicks == taps + 1, card.name + " accepts the next fresh tap immediately after a drag");
                }
            }
            finally
            {
                card.onClick = original;
                scroll.StopMovement(); scroll.content.anchoredPosition = initialPosition; flush();
            }
        }

        internal static void CheckBinding(Button card, object owner, string expectedMethod, Action<bool, string> require)
        {
            // The pinned Unity version stores runtime listeners in InvokableCall.Delegate.
            // Read the bound Action before gesture tests temporarily replace the event; never invoke a game or its store.
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var prepare = typeof(UnityEventBase).GetMethod("PrepareInvoke", flags);
            require(prepare != null, "Pinned Unity exposes runtime listeners for route verification");
            var calls = (IEnumerable)prepare.Invoke(card.onClick, null);
            var routes = new List<Delegate>();
            int listeners = 0;
            foreach (var call in calls)
            {
                listeners++;
                FieldInfo field = null;
                for (var type = call.GetType(); type != null && field == null; type = type.BaseType)
                    field = type.GetField("Delegate", flags | BindingFlags.DeclaredOnly);
                require(field != null, "Runtime entry listener exposes its delegate");
                CollectRoutes(field.GetValue(call) as Delegate, owner, routes, 0);
            }
            require(listeners == 1 && routes.Count == 1 && routes[0].Method.Name == expectedMethod,
                card.name + " retains its original " + expectedMethod + " callback on the production host");
        }

        private static void CollectRoutes(Delegate callback, object owner, List<Delegate> routes, int depth)
        {
            if (callback == null || depth > 4) return;
            foreach (var entry in callback.GetInvocationList())
            {
                if (ReferenceEquals(entry.Target, owner)) { routes.Add(entry); continue; }
                if (entry.Target is Delegate nested) { CollectRoutes(nested, owner, routes, depth + 1); continue; }
                if (entry.Target == null) continue;
                foreach (var field in entry.Target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                        CollectRoutes(field.GetValue(entry.Target) as Delegate, owner, routes, depth + 1);
            }
        }
    }

    internal sealed class LobbyTestInput : BaseInput
    {
        internal Vector2 Position;
        internal bool Held, Down, Up;
        public override bool mousePresent => true;
        public override Vector2 mousePosition => Position;
        public override Vector2 mouseScrollDelta => Vector2.zero;
        public override bool touchSupported => false;
        public override int touchCount => 0;
        public override bool GetMouseButton(int button) => button == 0 && Held;
        public override bool GetMouseButtonDown(int button) => button == 0 && Down;
        public override bool GetMouseButtonUp(int button) => button == 0 && Up;
        public override float GetAxisRaw(string axisName) => 0;
        public override bool GetButtonDown(string buttonName) => false;
    }
}
