# 属性面板几何编辑

选中一个独立的线段、矩形或圆，属性面板中可以直接输入几何参数。数字使用图像坐标，单位 px，坐标和尺寸按整数显示，不带小数点。显示舍入不改变图形内部的 double 精度；输入仍允许小数，提交后恢复整数显示。视图缩放和平移不会改变这些数值。

| 图形 | 编辑字段 | 操作规则 |
| --- | --- | --- |
| 线段 | 起点 X/Y、终点 X/Y | 只改变对应端点，长度自动刷新 |
| 线段 | 长度 | 固定起点和方向，改变终点 |
| 矩形 | 左上角 X/Y | 整体平移，保持宽高，附加文字一起移动 |
| 矩形 | 宽/高 | 固定实际左上角，改变右下角 |
| 圆 | 圆心 X/Y | 整体平移，保持半径，附加文字一起移动 |
| 圆 | 半径 | 固定圆心；只读直径同步刷新 |

## 输入操作

- 属性面板的 TextBox 获得焦点时自动全选内容；鼠标首次点击也全选，已聚焦后再次点击可正常放置光标。
- Enter 提交当前一行；焦点离开整行也尝试提交。
- 从同一行的 X 切到 Y、宽切到高时保留草稿，不提前修改图形。
- Esc 恢复当前行的真实值；它不是对已提交操作的撤销。
- 空文本、错误字符、非有限值、非正尺寸或不能表达的最终几何会显示错误，真实图形保持原状。
- 输入字段不包含 X/Y 前缀或单位。未编辑字段保留原 double 精度，不因显示格式写回舍入值。
- 切换选择取消旧图形的未提交草稿。外部代码或鼠标在编辑期间改变几何时，废弃旧草稿并提示重新输入。

坐标允许负值及超出图像范围；不会自动裁剪。长度、宽高、半径必须大于零。已有零长度线段需先输入有效端点建立方向，再修改长度。

锁定图形、隐藏图层、组合和多选不能使用几何编辑区。先解锁或解组再编辑。第一版支持精确类型的内置 Line、Rectangle、Circle，派生类需要后续显式适配。图形自身带非恒等 Transform 时几何区只读，恢复恒等后可重新编辑；普通视口 zoom/pan 不受影响。数值提交不应用鼠标吸附或 Toolbar 的 LineDirectionMode。

## 代码入口

`PropertyPanelTheme.xaml` 的三种图形模板嵌入 `ShapeGeometryEditorControl`。控件接收 `TargetShape` 和 `ShapeRepository`，保持模板的图形 DataContext，仅内部根容器使用编辑器 VM。原有 `IImageViewerViewModel` 接口无新增必需成员。

图形层提供：

```csharp
line.SetEndpoints(start, end);
rectangle.SetBounds(new Rect(topLeft, size));
circle.SetCircle(center, radius);
```

这些方法用于所属 Dispatcher 上已完成、未锁定的图形，先完整校验，再更新模型、绘制几何和手柄，最后发送属性通知并请求重绘。它们不会创建新 Shape、重新分配图层或改变选择。鼠标创建过程与原有 FromData 仍允许未完成及零尺寸状态。

纯位置修改通过 `IShapeRepository.TranslateShapes`。`ShapeVisualBase.ValidateTranslation` 允许仓库在移动任何成员之前验证锚点和附加文字不会产生非有限坐标。新增数值预验证不提供第三方 `TranslateCore` 抛异常后的完整快照回滚。

图形导出读取修改后的真实模型。“保存图层”仍只保存图层配置，不会保存图形几何；未新增通用 Undo/Redo 或图形文件格式。

## 验证

```powershell
dotnet test Test/Lan.SketchBoard.Tests/Lan.SketchBoard.Tests.csproj --no-restore --logger "trx;LogFileName=geometry-full.trx" --results-directory TestResults/GeometryEditing
dotnet test src/Lan.Shapes.Tests/Lan.Shapes.Tests.csproj --no-restore --logger "trx;LogFileName=geometry-data.trx" --results-directory TestResults/GeometryEditing
dotnet build Lan.Shapes.SimpleApp/Lan.Shapes.SimpleApp.csproj --no-restore
dotnet build Test/Lan.Shapes.TestApp/Lan.Shapes.App.csproj --no-restore
```

新增行为测试覆盖几何结果、通知回调时机、完整字段提交、精度、非法输入、外部同步、资格与生命周期、平移附加文本和极端数值。真实 WPF Window 的键盘焦点测试覆盖组内切换、离组提交、连续跨组输入、画布获焦后改选、错误修正和锁定时丢弃输入。

布局测试验证实际 260px 面板的字段边界和祖先绑定，并实际执行长度修改。截图保存在 `TestResults/GeometryEditing/property-panel-*.png` 与 `viewer-geometry-editing.png`；TRX 和截图目录被 Git 忽略。这些是自动 WPF 行为与渲染验证，不等同于在示例应用中逐项进行人工桌面操作。

2026-10-06 首次实现的验证结果：

| 检查 | 结果 | 本地记录 |
| --- | --- | --- |
| Lan.SketchBoard.Tests 完整套件 | 691 / 691 通过，含 57 项新增几何编辑测试 | `TestResults/GeometryEditing/geometry-final-suite.trx` |
| Lan.Shapes.Tests 数据测试 | 9 / 9 通过 | `TestResults/GeometryEditing/geometry-shapes-suite.trx` |
| Prism 示例 Lan.Shapes.SimpleApp | 构建成功，0 错误 | `TestResults/GeometryEditing/geometry-simpleapp-build.log` |
| MSDI 示例 Lan.Shapes.App | 构建成功，0 错误 | `TestResults/GeometryEditing/geometry-testapp-build.log` |
| XAML XML 语法及修改文件空白检查 | 通过 | 工具输出 |

最终完整测试编译时仍报告原有 `ImageViewerControl.xaml.cs` 和 `ImageViewer.cs` 的 CS8632 nullable 上下文警告；两个示例的最终增量构建均为 0 警告。未改动这些无关代码。

生命周期回归同时覆盖首次加载前不创建编辑器、加载后建立订阅、卸载后解除订阅，以及旧编辑器拒绝提交。编辑器只在控件已加载时建立，避免从未挂载的控件持有静态渲染事件订阅。

2026-10-10 按用户要求调整显示和焦点操作：几何输入与只读坐标、直径统一按整数显示；属性面板的 TextBox 通过公共样式启用 `TextBoxFocusBehavior.SelectAllOnFocus`。首次鼠标点击获取焦点时阻止默认点击折叠选区，已聚焦后的点击保持正常行为。原 double 保留在字段中，未修改的输入不会因整数显示被写回舍入值。

本次完整 `Lan.SketchBoard.Tests` 套件 **698 / 698 通过**，记录为 `TestResults/GeometryEditing/geometry-display-focus-final.trx`。新增及扩展回归覆盖三类图形的整数显示与精度保持、小数提交后的显示、Tab/程序焦点、重新聚焦、首次鼠标点击，以及 Tag 输入框的样式继承。
