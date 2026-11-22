# 抓大鹅原型实施计划

目标是验证同一批烘焙物件在直壁和收底容器里的遮挡、拾取和翻动。

1. `Tests/BakeryBasket` 验证固定种子配货、七槽内同类三消、满槽和清空边界。`Assets/Scripts/Core/BakeryBasket/BakeryRound.cs` 提供独立规则。
2. 录入 Quaternius 的八个 FBX 和许可。`BakeryWorld.cs` 持有独立物理场景、相机、两种碗型以及刚体和材质。两种碗型共用配货、落点和颠篮动作。
3. `BakeryMesh.cs` 用屏幕光线检测最前面的实际模型表面。`BakeryPage.cs` 接按下描边、松手拾取、模型飞入槽、三消、重置和切换篮型。颠篮前后采样同一批底部小件的可点状态。
4. `AppController` 添加独立大厅入口，退出时释放场景。Unity Editor 检查和隔离 Android 验证包检查开局、命中、高亮、入槽、颠篮和切换。
