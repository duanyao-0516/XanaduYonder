this.autoDestroyAtClosed = true;
this.list.scrollBar.hide = true;
this.list.scrollBar.elasticDistance = 150;
this.mainBtn.on(Laya.Event.CLICK, this, function () { N.PlaySound('btn'); D.ShowUIPanel('mainScene'); });
this.useDaoId = e.GetUserData('useDaoId');
this.renderItems();
