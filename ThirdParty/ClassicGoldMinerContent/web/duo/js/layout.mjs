// Layout changes never mutate a previously validated save or source map.
// Wide fields preserve source values and sprite proportions; longer horizontal
// travel changes geometry, so identical original difficulty is not claimed.
export const BASE_WIDTH = 550;
export const GAME_HEIGHT = 400;
export function playWidth(cssWidth, cssHeight) {
  if (!(cssWidth > 0 && cssHeight > 0)) return BASE_WIDTH;
  return Math.max(BASE_WIDTH, Math.min(1600, Math.round(GAME_HEIGHT * cssWidth / cssHeight)));
}
export function heroX(player, width) {
  // Keep the fixed grip offset unscaled, so the rope origin follows the same
  // horizontal projection as the original map rather than drifting by 15px.
  return ([185, 285][player] * width / BASE_WIDTH) + 15;
}
export function projectAngle(degrees, ratio) {
  const angle = degrees * Math.PI / 180;
  return Math.atan2(Math.sin(angle) * ratio, Math.cos(angle)) * 180 / Math.PI;
}
export function reflowPlay(source, width) {
  const previous = source.viewportWidth ?? BASE_WIDTH;
  if (!Number.isInteger(width) || width < BASE_WIDTH || width > 1600 ||
      !Number.isInteger(previous) || previous < BASE_WIDTH || previous > 1600)
    throw new Error('双人存档矿场宽度无效，原存档已保留。');
  const play = JSON.parse(JSON.stringify(source));
  play.viewportWidth = width;
  const ratio = width / previous;
  play.heroes.forEach((hero, index) => {
    const original = source.heroes[index];
    hero.aimAngle = original.aimAngle ?? projectAngle(original.hook.angle, BASE_WIDTH / previous);
    if (ratio === 1) return;
    const oldOrigin = heroX(index, previous) - 15;
    const newOrigin = heroX(index, width) - 15;
    const mapX = x => newOrigin + (x - oldOrigin) * ratio;
    hero.hook.x = mapX(original.hook.x);
    hero.hook.angle = projectAngle(hero.aimAngle, width / BASE_WIDTH);
    // Preserve speed magnitude and return direction while reprojecting the
    // old viewport vector once. Canonical aim controls the idle swing phase.
    const magnitude = Math.hypot(original.grabSin, original.grabCos);
    const length = Math.hypot(original.grabSin * ratio, original.grabCos);
    hero.grabSin = length ? original.grabSin * ratio * magnitude / length : 0;
    hero.grabCos = length ? original.grabCos * magnitude / length : 0;
    if (hero.resetRemaining !== null) {
      // Original engine intentionally keeps these points stale during reset.
      hero.hook.grabPoint.x = mapX(original.hook.grabPoint.x);
      hero.hook.rope.x = mapX(original.hook.rope.x);
    } else {
      const angle = -hero.hook.angle * Math.PI / 180;
      hero.hook.rope = { x: hero.hook.x + Math.sin(angle) * 25, y: hero.hook.y + Math.cos(angle) * 25 };
      hero.hook.grabPoint = { x: hero.hook.rope.x, y: hero.hook.rope.y + 5 };
    }
  });
  if (ratio !== 1) {
    for (const ore of play.ores) if (ore.show) ore.x *= ratio;
    for (const hero of play.heroes) {
      if (hero.hook.oreId === null || hero.resetRemaining !== null) continue;
      const caught = play.ores.find(ore => ore.id === hero.hook.oreId);
      if (caught) { caught.x = hero.hook.grabPoint.x; caught.y = hero.hook.grabPoint.y; }
    }
  }
  return play;
}
export function minePlayer(clientX, clientY, rect, width, hudBottom = 90, safe = {}) {
  if (!(rect.width > 0 && rect.height > 0)) return null;
  const px = clientX - rect.left, py = clientY - rect.top;
  if (px < (safe.left || 0) || px > rect.width - (safe.right || 0) ||
      py < (safe.top || 0) || py > rect.height - (safe.bottom || 0)) return null;
  const x = px * width / rect.width;
  const y = (clientY - rect.top) * GAME_HEIGHT / rect.height;
  if (x < 0 || x > width || y < hudBottom || y > GAME_HEIGHT) return null;
  return x < width / 2 ? 0 : 1;
}
