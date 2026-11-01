(function () {
    'use strict';
    var saved = {}, pendingMode;
    var dialog = document.getElementById('new-confirm');
    ['solo', 'duo'].forEach(function (mode) {
        var status = document.getElementById(mode + '-status');
        try {
            saved[mode] = ClassicHost.read(mode);
            status.textContent = saved[mode] ? '已有进度，可继续上次游戏' : '还没有进行中的游戏';
            document.querySelector('[data-mode="' + mode + '"][data-action="resume"]').disabled = !saved[mode];
        } catch (failure) {
            saved[mode] = { unreadable: true };
            status.textContent = '存档无法读取，原文件已保留';
            ClassicHost.message(failure.message);
        }
    });
    function enter(mode, action) { location.href = mode + '/index.html?' + action + '=1'; }
    document.querySelectorAll('[data-mode]').forEach(function (button) {
        button.addEventListener('click', function () {
            var mode = button.dataset.mode, action = button.dataset.action;
            if (action === 'new' && saved[mode]) {
                pendingMode = mode;
                document.getElementById('new-description').textContent = '这会替换' + (mode === 'solo' ? '单人' : '双人') + '模式的现有进度，另一模式不受影响。';
                dialog.showModal();
            } else enter(mode, action);
        });
    });
    document.getElementById('new-cancel').addEventListener('click', function () { dialog.close(); pendingMode = null; });
    document.getElementById('new-start').addEventListener('click', function () { if (pendingMode) enter(pendingMode, 'new'); });
})();
