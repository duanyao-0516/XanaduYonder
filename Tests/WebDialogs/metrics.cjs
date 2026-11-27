// Measure the rendered settings sheet inside the real game viewport.
const assert = require('node:assert/strict');

async function settingsMetrics(card, actionSelector, leftInset = 0, rightInset = 0) {
  const metrics = await card.evaluate((surface, selector) => {
    const row = surface.querySelector('.xy-switch');
    const label = row.querySelector('.xy-switch-label');
    const textBounds = document.createRange();
    textBounds.selectNodeContents(label);
    const track = row.querySelector('.switch-track, .xy-switch-input');
    const action = surface.querySelector(selector);
    const font = el => parseFloat(getComputedStyle(el).fontSize);
    const css = getComputedStyle(surface);
    return {
      width: window.innerWidth, height: window.innerHeight,
      left: parseFloat(css.paddingLeft), right: parseFloat(css.paddingRight),
      heading: font(surface.querySelector('h2')), label: font(label), action: font(action),
      rowHeight: row.getBoundingClientRect().height, actionHeight: action.getBoundingClientRect().height,
      switchGap: track.getBoundingClientRect().left - textBounds.getBoundingClientRect().right
    };
  }, actionSelector);
  const {width,height} = metrics;
  const gutter = width >= 480 && width > height ? 24 : width >= 360 ? 20 : 16;
  assert.equal(metrics.heading,16);
  assert.equal(metrics.label,14);
  assert.equal(metrics.action,14);
  assert.equal(metrics.left,Math.max(gutter+leftInset,(width-800)/2));
  assert.equal(metrics.right,Math.max(gutter+rightInset,(width-800)/2));
  assert(metrics.rowHeight >= 44 && metrics.actionHeight >= 44);
  assert(metrics.switchGap >= 0 && metrics.switchGap <= 14, 'label and switch must stay together');
  return metrics;
}
// Send native touch input through Chromium's compositor, as a finger drag would.
async function swipeSettings(page, scroller) {
  const {height, overflow, offset, top, left, width} = await scroller.evaluate(node => {
    node.scrollTop = 0;
    const rect = node.getBoundingClientRect();
    return {height:rect.height, overflow:node.scrollHeight-node.clientHeight, offset:node.scrollTop,
      top:rect.top, left:rect.left, width:rect.width};
  });
  assert(overflow > 1 && height > 26, 'missing usable scrolling area: ' + JSON.stringify({height,overflow,offset,top}));
  await swipeUp(page,left+width*.32,top+height*.8,height*.6);
  const handle = await scroller.elementHandle();
  await page.waitForFunction(({node, offset}) => node.scrollTop > offset+1, {node:handle,offset}, {timeout:3000});
  return await scroller.evaluate(node => node.scrollTop);
}

async function swipeUp(page, x, start, travel) {
  const session = await page.context().newCDPSession(page);
  try {
    await session.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x,y:start}]});
    for (let step = 1; step <= 8; step++) {
      await session.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x,y:start-travel*step/8}]});
      await page.waitForTimeout(18);
    }
    await session.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
  } finally {
    await session.detach();
  }
}

module.exports = { settingsMetrics, swipeSettings, swipeUp };
