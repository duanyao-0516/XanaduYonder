// Sample the rendered layer at deterministic points on the browser's real CSS animations.
const assert = require('node:assert/strict');
const {durationMs} = require('../../Assets/Resources/DialogSheetMotion.json');

async function sheetMotion(card) {
  const handle = await card.evaluateHandle(node => {
    const layer = node.parentElement;
    const measured = opening => {
      const direction = opening ? 'in' : 'out';
      const slide = node.getAnimations().find(a => a.animationName === 'xy-sheet-' + direction);
      const shade = layer.getAnimations().find(a => a.animationName === 'xy-scrim-' + direction);
      if (!slide || !shade) throw new Error('Missing sheet or backdrop animation on ' + direction);
      const duration = slide.effect.getTiming().duration;
      const samples = [];
      const running = [slide, shade].map(animation => ({animation, time:animation.currentTime}));
      for (const part of [0,.5,1]) {
        for (const {animation} of running) {
          animation.pause();
          animation.currentTime = part * duration;
        }
        const box = node.getBoundingClientRect();
        const rgba = getComputedStyle(layer).backgroundColor.match(/[\d.]+/g).map(Number);
        samples.push({top:box.top, height:box.height, alpha:rgba.length === 4 ? rgba[3] : 1});
      }
      for (const {animation,time} of running) {
        if (opening) animation.finish();
        else { animation.currentTime = time; animation.play(); }
      }
      return {duration, shadeDuration:shade.effect.getTiming().duration, samples};
    };
    const entry = measured(true);
    const exit = new Promise((resolve,reject) => {
      function start(event) {
        if (event.target !== node || event.animationName !== 'xy-sheet-out') return;
        layer.removeEventListener('animationstart', start);
        try { resolve(measured(false)); } catch (error) { reject(error); }
      }
      layer.addEventListener('animationstart', start);
    });
    return {entry,exit};
  });
  function verify(data, opening) {
    assert.equal(data.duration, durationMs);
    assert.equal(data.shadeDuration, durationMs);
    const [off,mid,on] = opening ? data.samples : [...data.samples].reverse();
    assert(Math.abs(off.top-on.top-on.height) < 1, 'the panel must travel its complete height');
    assert(mid.top > on.top && mid.top < off.top, 'missing intermediate panel position');
    assert(off.alpha < .005 && mid.alpha > off.alpha && mid.alpha < on.alpha && Math.abs(on.alpha-.4) < .005,
      'backdrop opacity must move with the panel');
  }
  verify(await handle.evaluate(value => value.entry),true);
  return async () => {
    try {
      const exit = await handle.evaluate(value => Promise.race([value.exit,
        new Promise((_,reject)=>setTimeout(()=>reject(new Error('No sheet exit animation')),2500))]));
      verify(exit,false);
    } finally { await handle.dispose(); }
  };
}
module.exports = {sheetMotion};
