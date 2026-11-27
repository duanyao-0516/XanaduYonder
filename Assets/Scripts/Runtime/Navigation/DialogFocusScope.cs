using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    [ExecuteAlways]
    internal sealed class DialogFocusScope : MonoBehaviour
    {
        private readonly List<CanvasGroup> blocked = new List<CanvasGroup>();
        private readonly List<Button> focusTargets = new List<Button>();
        private Transform host;
        private GameObject previousSelection;
        private bool acquired;

        // The scrim catches pointers. Background selectables also need to reject Submit and navigation.
        public void BlockBackground(Transform host)
        {
            this.host = host;
            if (isActiveAndEnabled) Acquire();
        }

        private void OnEnable()
        {
            if (host != null) Acquire();
        }

        private void Acquire()
        {
            if (acquired) return;
            acquired = true;
            var events = EventSystem.current;
            if (events != null && !events.alreadySelecting)
            {
                previousSelection = events.currentSelectedGameObject;
                events.SetSelectedGameObject(null);
            }
            foreach (Transform child in host)
            {
                if (child == transform || !child.gameObject.activeInHierarchy || child.GetComponentInChildren<Selectable>() == null)
                    continue;
                var group = child.GetComponent<CanvasGroup>();
                if (group == null) group = child.gameObject.AddComponent<CanvasGroup>();
                if (!group.interactable) continue;
                blocked.Add(group);
                group.interactable = false;
            }
        }

        private void LateUpdate()
        {
            var events = EventSystem.current;
            if (events == null || !events.isFocused || events.alreadySelecting) return;
            var selected = events.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(transform)) return;
            GetComponentsInChildren(false, focusTargets);
            foreach (var control in focusTargets)
            {
                if (control.navigation.mode == Navigation.Mode.None || !control.IsInteractable()) continue;
                events.SetSelectedGameObject(control.gameObject);
                break;
            }
        }

        private void OnDisable()
        {
            if (!acquired) return;
            acquired = false;
            foreach (var group in blocked)
                if (group != null) group.interactable = true;
            blocked.Clear();
            focusTargets.Clear();
            var events = EventSystem.current;
            if (events != null && !events.alreadySelecting && previousSelection != null && previousSelection.activeInHierarchy)
            {
                var target = previousSelection.GetComponent<Selectable>();
                if (target != null && target.IsInteractable()) events.SetSelectedGameObject(previousSelection);
            }
            previousSelection = null;
        }
    }
}
