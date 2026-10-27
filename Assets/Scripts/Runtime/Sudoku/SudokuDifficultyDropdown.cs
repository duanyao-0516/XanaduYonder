using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.Sudoku
{
    internal sealed class SudokuDifficultyDropdown : Dropdown
    {
        private GameObject expandedList;
        public Func<bool> CanOpen;
        public bool IsExpanded { get { return expandedList != null; } }
        public override void OnPointerClick(PointerEventData eventData)
        {
            if (CanOpen != null && CanOpen()) base.OnPointerClick(eventData);
        }
        public override void OnSubmit(BaseEventData eventData)
        {
            if (CanOpen != null && CanOpen()) base.OnSubmit(eventData);
        }
        protected override GameObject CreateDropdownList(GameObject template)
        {
            expandedList = base.CreateDropdownList(template);
            return expandedList;
        }
        protected override void DestroyDropdownList(GameObject list)
        {
            base.DestroyDropdownList(list); expandedList = null;
        }
    }
}
