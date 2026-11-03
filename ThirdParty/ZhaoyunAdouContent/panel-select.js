/* A page-local selector keeps the Android WebView focused throughout a choice. */
(function () {
  'use strict';
  let nextId = 0;

  function choiceText(item) {
    const copy = document.createElement('span');
    copy.className = 'panel-option-copy';
    const title = document.createElement('span');
    title.className = 'panel-option-title';
    const name = document.createElement('strong');
    name.textContent = item.name;
    title.append(name);
    if (item.typeLabel) {
      const type = document.createElement('span');
      type.className = 'panel-option-type';
      type.textContent = item.typeLabel;
      title.append(type);
    }
    if (item.rarityName) {
      const quality = document.createElement('span');
      quality.className = 'panel-option-quality';
      quality.style.color = item.rarityColor;
      quality.textContent = item.rarityName;
      title.append(quality);
    }
    copy.append(title);
    if (item.description) {
      const description = document.createElement('span');
      description.className = 'panel-option-description';
      description.textContent = item.description;
      copy.append(description);
    }
    return copy;
  }

  function create(parent, title) {
    const element = document.createElement('details');
    element.className = 'save-editor-field panel-select';
    const summary = document.createElement('summary');
    summary.setAttribute('role', 'combobox');
    summary.setAttribute('aria-label', title);
    summary.setAttribute('aria-haspopup', 'listbox');
    summary.setAttribute('aria-expanded', 'false');
    const caption = document.createElement('span');
    caption.textContent = title;
    const displayed = document.createElement('span');
    displayed.className = 'panel-select-value';
    summary.append(caption, displayed);

    const list = document.createElement('div');
    list.className = 'panel-select-options';
    list.id = 'xanadu-panel-options-' + (++nextId);
    list.setAttribute('role', 'listbox');
    list.setAttribute('aria-label', title);
    summary.setAttribute('aria-controls', list.id);
    element.append(summary, list);
    parent.append(element);
    let value = '';

    function select(option) {
      value = option?.value ?? '';
      displayed.replaceChildren();
      if (option) displayed.append(option.firstElementChild.cloneNode(true));
      for (const row of list.children) {
        row.setAttribute('aria-selected', String(row === option));
      }
    }

    function setOptions(choices, preferred = value) {
      list.replaceChildren();
      for (const item of choices) {
        const option = document.createElement('button');
        option.type = 'button';
        option.value = String(item.id);
        option.append(choiceText(item));
        option.setAttribute('role', 'option');
        list.append(option);
      }
      select(Array.from(list.children).find(row => row.value === String(preferred)) || list.firstElementChild);
      element.open = false;
    }

    element.addEventListener('toggle', () => {
      summary.setAttribute('aria-expanded', String(element.open));
      if (element.open) {
        for (const other of element.closest('.save-editor-card').querySelectorAll('.panel-select[open]')) {
          if (other !== element) other.open = false;
        }
        const selected = list.querySelector('[aria-selected="true"]');
        if (selected) list.scrollTop += selected.getBoundingClientRect().top - list.getBoundingClientRect().top;
      }
    });
    list.addEventListener('click', event => {
      const option = event.target.closest('[role="option"]');
      if (!option) return;
      const changed = option.value !== value;
      select(option);
      element.open = false;
      summary.focus({preventScroll:true});
      if (changed) element.dispatchEvent(new Event('change'));
    });
    element.addEventListener('keydown', event => {
      if (event.key === 'Escape' && element.open) {
        event.preventDefault();
        element.open = false;
        summary.focus();
      } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        event.preventDefault();
        const row = document.activeElement;
        const next = list.contains(row)
          ? (event.key === 'ArrowDown' ? row.nextElementSibling : row.previousElementSibling) || row
          : list.querySelector('[aria-selected="true"]');
        element.open = true;
        next?.focus();
      }
    });
    return {element, setOptions, get value() { return value; }};
  }

  window.XanaduPanelSelect = Object.freeze({create});
})();
