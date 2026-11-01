/* Pure horizontal geometry migration. Sprite dimensions and source maps stay intact. */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) module.exports = factory(require('./state.js'));
    else root.ClassicSoloLayout = factory(root.ClassicSoloState);
}(typeof window !== 'undefined' ? window : this, function (stateAPI) {
    'use strict';
    function validWidth(width) {
        if (!Number.isInteger(width) || width < 640 || width > 1920) throw new Error('矿场宽度无效');
        return width;
    }
    function viewportWidth(width, height) {
        if (!(width > 0) || !(height > 0)) return 640;
        return Math.max(640, Math.min(1920, Math.round(480 * width / height)));
    }
    function projectRotation(angle, ratio) { return Math.atan2(Math.sin(angle) * ratio, Math.cos(angle)); }
    function unprojectRotation(angle, ratio) { return projectRotation(angle, 1 / ratio); }
    function scaleMovement(tween, ratio) {
        if (!tween) return;
        ['vStart','vEnd','vStartCache','vEndCache'].forEach(function (key) {
            if (Object.prototype.hasOwnProperty.call(tween[key], 'x')) tween[key].x *= ratio;
        });
    }
    function reflowSnapshot(source, width) {
        validWidth(width);stateAPI.validate(source);
        var state = JSON.parse(JSON.stringify(source));
        if (state.scene !== 'Game') return state;
        var game = state.game, oldWidth = game.viewportWidth === undefined ? 640 : game.viewportWidth;
        var ratio = width / oldWidth, pod = game.pod;
        if (pod.canonicalRotation === undefined) pod.canonicalRotation = unprojectRotation(pod.sprite.rotation, oldWidth / 640);
        game.viewportWidth = width;
        if (ratio === 1) return state;
        game.boulders.forEach(function (boulder) {
            boulder.sprite.x *= ratio;scaleMovement(boulder.movement, ratio);
            // definition remains the original 640-wide source map, never a reflowed map.
        });
        pod.sprite.x *= ratio;
        pod.sprite.rotation = projectRotation(pod.sprite.rotation, ratio);
        pod.miner.x += (width - oldWidth) / 2; // Animation offsets from the home anchor are local.
        pod.startRopeX *= ratio;
        pod.currentDir = Math.atan2(Math.sin(pod.currentDir), Math.cos(pod.currentDir) * ratio);
        pod.hotSpotX *= ratio;
        if (pod.status === 'shoot' || pod.status === 'rewind') {
            // The claw remains its original size, so its 40-unit tip offset is local.
            pod.hotSpotX = pod.sprite.x + 40 * Math.cos(pod.currentDir);
            pod.hotSpotY = pod.sprite.y + 40 * Math.sin(pod.currentDir);
        }
        // wave is parented to the claw: its local coordinates must not expand.
        game.boom.x *= ratio;
        if (game.reward) game.reward.startX *= ratio;
        return stateAPI.validate(state);
    }
    return {viewportWidth:viewportWidth,projectRotation:projectRotation,unprojectRotation:unprojectRotation,scaleMovement:scaleMovement,reflowSnapshot:reflowSnapshot};
}));
