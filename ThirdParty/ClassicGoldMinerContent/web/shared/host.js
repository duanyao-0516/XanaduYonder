(function (global) {
    'use strict';
    var native = global.ClassicGoldMinerNative;
    var adapter = null, paused = false, lastError = '', toastTimer;
    var MAX_SAVE_BYTES = 2 * 1024 * 1024;
    function validMode(mode) {
        if (mode !== 'solo' && mode !== 'duo') throw new Error('未知的游戏模式');
        return mode;
    }
    function validate(mode, state) {
        validMode(mode);
        if (!state || typeof state !== 'object' || Array.isArray(state) || state.schema !== 1 || state.mode !== mode)
            throw new Error('存档版本或模式不匹配');
        return state;
    }
    function message(text) {
        if (!document.body) return;
        var node = document.getElementById('classic-message');
        if (!node) {
            node = document.createElement('div');
            node.id = 'classic-message';
            node.setAttribute && node.setAttribute('role', 'status');
            document.body.appendChild(node);
        }
        node.textContent = text;
        node.style.display = 'block';
        global.clearTimeout(toastTimer);
        toastTimer = global.setTimeout(function () { node.style.display = 'none'; }, 6000);
    }
    function error(reason) {
        lastError = reason && reason.message ? reason.message : String(reason);
        message('未能保存进度：' + lastError);
        return false;
    }
    function read(mode) {
        validMode(mode);
        var raw = native ? native.readSave(mode) : global.localStorage.getItem('classic-gold-miner-' + mode + '-v1');
        if (!raw) {
            if (native && native.getLastSaveError()) throw new Error(native.getLastSaveError());
            return null;
        }
        if (raw.length > MAX_SAVE_BYTES) throw new Error('存档过大，原文件已保留');
        try { return validate(mode, JSON.parse(raw)); }
        catch (failure) { throw new Error('存档无法读取，原文件已保留。' + failure.message); }
    }
    function save(mode, state) {
        try {
            validate(mode, state);
            var json = JSON.stringify(state, function (key, value) {
                if (typeof value === 'number' && !Number.isFinite(value)) throw new Error('存档包含无效数值');
                if (typeof value === 'undefined' || typeof value === 'function') throw new Error('存档包含未定义状态');
                return value;
            });
            if (json.length > MAX_SAVE_BYTES) throw new Error('存档过大');
            if (native) {
                if (!native.writeSave(mode, json)) throw new Error(native.getLastSaveError() || '写入失败，请重试');
            } else global.localStorage.setItem('classic-gold-miner-' + mode + '-v1', json);
            lastError = '';
            return true;
        } catch (failure) { return error(failure); }
    }
    function checkpoint() {
        if (!adapter) return true;
        try {
            var state = adapter.capture();
            // A menu/loading screen has no new run to replace the saved one.
            return state === null ? true : save(adapter.mode, state);
        } catch (failure) { return error(failure); }
    }
    function pauseAndSave() {
        paused = true;
        try { if (adapter) adapter.pause(); }
        catch (failure) { return error(failure); }
        return checkpoint();
    }
    function foreground() {
        if (!paused || !adapter || !document.body || document.getElementById('classic-resume-overlay')) return;
        var overlay = document.createElement('div');
        overlay.id = 'classic-resume-overlay';
        var button = document.createElement('button');
        button.textContent = '继续游戏';
        button.addEventListener('click', function () {
            try {
                adapter.resume();
                paused = false;
                overlay.remove();
            } catch (failure) {
                // A failed adapter stays paused. Reveal its recovery/menu controls;
                // an unrecoverable run cannot be repaired by a permanent Continue overlay.
                overlay.remove();
                error(failure);
            }
        });
        overlay.appendChild(button);
        document.body.appendChild(overlay);
    }
    var soundEnabled = true;
    try { if (native) soundEnabled = !!native.isSoundEnabled(); } catch (ignored) { /* Browser preview has no native settings. */ }
    global.ClassicHost = {
        soundEnabled: soundEnabled,
        read: read,
        save: save,
        register: function (value) {
            validMode(value.mode);
            if (typeof value.capture !== 'function' || typeof value.pause !== 'function' || typeof value.resume !== 'function')
                throw new Error('游戏存档接口不完整');
            adapter = value;
            if (paused || document.hidden) {
                paused = true;
                adapter.pause();
                if (!document.hidden) foreground();
            }
        },
        checkpoint: checkpoint,
        pauseAndSave: pauseAndSave,
        foreground: foreground,
        message: message,
        getLastError: function () { return lastError; }
    };
    global.setInterval(function () { if (!paused && !document.hidden) checkpoint(); }, 1000);
    document.addEventListener('visibilitychange', function () {
        if (document.hidden) pauseAndSave();
        else foreground();
    });
    global.addEventListener('pagehide', pauseAndSave);
})(window);
