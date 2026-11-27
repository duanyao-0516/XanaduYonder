/* The owner supplies Cancel/Back. A tap that started on the card never dismisses it. */
(() => {
  'use strict';
  const finishing = new WeakMap();
  const cancellations = new WeakMap();
  function bindBackdrop(layer, dismiss) {
    cancellations.set(layer, dismiss);
    let pointer = null;
    layer.addEventListener('pointerdown', event => {
      pointer = event.isPrimary && event.target === layer ? event.pointerId : null;
    });
    layer.addEventListener('pointercancel', () => { pointer = null; });
    layer.addEventListener('click', event => {
      const outside = pointer !== null && event.target === layer;
      pointer = null;
      if (outside && !finishing.has(layer)) {
        event.stopPropagation();
        dismiss();
      }
    });
    layer.addEventListener('keydown', event => {
      if (event.key === 'Escape' && !event.defaultPrevented) {
        event.preventDefault();
        event.stopPropagation();
        if (!event.repeat && !finishing.has(layer)) dismiss();
      }
    });
  }
  function finishSheet(layer, done) {
    if (finishing.has(layer)) return;
    if (!layer.classList.contains('xy-sheet') || matchMedia('(prefers-reduced-motion: reduce)').matches) {
      done();
      return;
    }
    const card = layer.firstElementChild;
    card.inert = true;
    layer.classList.add('xy-closing');
    finishing.set(layer, setTimeout(() => {
      reset(layer);
      done();
    }, 200));
  }
  function reset(layer) {
    clearTimeout(finishing.get(layer));
    finishing.delete(layer);
    layer.classList.remove('xy-closing');
    if (layer.firstElementChild) layer.firstElementChild.inert = false;
  }
  function dismissTop() {
    const visible = [...document.querySelectorAll('.xy-dialog')].reverse().find(node =>
      cancellations.has(node) && node.getClientRects().length && getComputedStyle(node).visibility === 'visible');
    if (!visible) return false;
    if (!finishing.has(visible)) cancellations.get(visible)();
    return true;
  }
  window.XanaduDialogs = Object.freeze({bindBackdrop, finishSheet, reset, dismissTop});
})();
