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
  function durationMs() {
    if (matchMedia('(prefers-reduced-motion: reduce)').matches) return 0;
    return parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--xy-sheet-duration')) || 0;
  }
  function finishSheet(layer, done) {
    if (finishing.has(layer)) return;
    const duration = durationMs();
    if (!layer.classList.contains('xy-sheet') || !duration) {
      done();
      return;
    }
    const card = layer.firstElementChild;
    layer.style.setProperty('--xy-leave-transform', getComputedStyle(card).transform);
    layer.style.setProperty('--xy-leave-scrim', getComputedStyle(layer).backgroundColor);
    card.inert = true;
    layer.classList.add('xy-closing');
    const pending = {};
    finishing.set(layer, pending);
    const complete = () => {
      if (finishing.get(layer) !== pending) return;
      reset(layer);
      done();
    };
    const motion = card.getAnimations?.().find(animation => animation.animationName === 'xy-sheet-out');
    if (motion) motion.finished.then(complete, complete);
    else pending.timer = setTimeout(complete, duration);
  }
  function reset(layer) {
    clearTimeout(finishing.get(layer)?.timer);
    finishing.delete(layer);
    layer.classList.remove('xy-closing');
    layer.style.removeProperty('--xy-leave-transform');
    layer.style.removeProperty('--xy-leave-scrim');
    if (layer.firstElementChild) layer.firstElementChild.inert = false;
  }
  function dismissTop() {
    const visible = [...document.querySelectorAll('.xy-dialog')].reverse().find(node =>
      cancellations.has(node) && node.getClientRects().length && getComputedStyle(node).visibility === 'visible');
    if (!visible) return false;
    if (!finishing.has(visible)) cancellations.get(visible)();
    return true;
  }
  window.XanaduDialogs = Object.freeze({bindBackdrop, finishSheet, reset, dismissTop, durationMs});
})();
