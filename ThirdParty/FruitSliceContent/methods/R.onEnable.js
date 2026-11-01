this.chuangguanBtn.on(Laya.Event.CLICK, this, function () { N.PlaySound('btn'); N.model=1; D.ShowUIPanel('selectMap'); });
this.wujinBtn.on(Laya.Event.CLICK, this, function () { N.PlaySound('btn'); N.model=2; this.loadMap(function () { D.ShowUIPanel('gameScene'); }); });
this.timeBtn.on(Laya.Event.CLICK, this, function () { N.PlaySound('btn'); N.model=3; this.loadMap(function () { D.ShowUIPanel('gameScene'); }); });
this.shopBtn.on(Laya.Event.CLICK, this, function () { N.PlaySound('btn'); D.ShowUIPanel('shopScene'); });
window.XanaduFruitContent.ready();
