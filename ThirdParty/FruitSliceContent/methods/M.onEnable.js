this.autoDestroyAtClosed = true;
this.timeModelHeightNum = e.GetUserData('timeModelHeightNum');
this.wujinModelHeightNum = e.GetUserData('wujinModelHeightNum');
const leave = function (next) {
  if (!window.XanaduFruitContent.once(this, 'leave')) return;
  N.PlaySound('btn'); next();
}.bind(this);
this.mainBtn.on(Laya.Event.CLICK, this, function () { leave(function () { D.ShowUIPanel('mainScene'); }); });
this.nextBtn.on(Laya.Event.CLICK, this, function () { leave(function () {
  N.selectMapIndex = (N.selectMapIndex + 1) % e.GetUserData('guanka').length;
  D.ShowUIPanel('gameScene');
}); });
[this.fuhuoBtn, this.fuhuo2Btn, this.fuhuo3Btn].forEach(function (button) {
  button.on(Laya.Event.CLICK, this, function () { leave(function () { N.fuhuo=true; D.ShowUIPanel('gameScene'); }); });
}, this);
[this.chongwanBtn, this.chongxinBtn, this.chongxin3Btn].forEach(function (button) {
  button.on(Laya.Event.CLICK, this, function () { leave(function () { N.fuhuo=false; D.ShowUIPanel('gameScene'); }); });
}, this);
