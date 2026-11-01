if (!Number.isInteger(t) || t < 0 || t >= 27) return;
N.PlaySound('btn');
this.useDaoId = t;
e.SetUserData('useDaoId', t);
this.renderItems();
