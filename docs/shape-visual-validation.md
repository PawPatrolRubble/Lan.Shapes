# ShapeVisualBase 修复与验证记录

验证日期：2026-10-05。目标框架：.NET 8 / WPF。

## 已落实的契约

- 选择、悬停和锁定分别保存；显示状态按 Locked、Selected、MouseOver、Normal 的顺序解析。悬停不会清除选择，锁定也不会丢失已有选择。
- 基类统一管理绘制上下文、裁剪、正文、句柄和文字的绘制顺序。内置和自定义形状通过绘制钩子扩展；`UpdateVisual` 保留 virtual 以兼容外部派生类。
- `RenderGeometry` 是形状正文的几何来源。模型边界、创建完成判定和框选使用正文几何，句柄与文字装饰不扩大框选范围。
- 框选先在形状坐标中合并正文与描边，再应用绘制裁剪、Visual 裁剪和坐标变换。旋转、非均匀缩放、粗描边和被裁掉的正文都有回归用例。
- 取消交互清除按下点、平移点、句柄和拖动状态。画板失去捕获后忽略残留的按住移动和松开事件，新的按下事件恢复操作。
- 缩放相关刷新合并成一次绘制；创建完成立即移除创建句柄。自定义形状的文字和粗线样式使用局部副本，避免修改共享 Pen/Brush。

兼容性约定：旧的 `State = Selected` 调用仍会解锁；新代码应分别操作选择和锁定。`TextGeometry` 的既有 X 镜像与 700 平移仍属于原坐标约定，改变这项约定需要单独核实导入/导出调用方。

## 自动化验证

在仓库根目录执行：

```powershell
dotnet test Test/Lan.SketchBoard.Tests/Lan.SketchBoard.Tests.csproj --no-restore --logger "trx;LogFileName=shape-visual-main.trx" --results-directory TestResults/ShapeVisual
dotnet test src/Lan.Shapes.Tests/Lan.Shapes.Tests.csproj --no-restore --logger "trx;LogFileName=shape-visual-data.trx" --results-directory TestResults/ShapeVisual
dotnet build Test/Lan.Shapes.TestApp/Lan.Shapes.App.csproj --no-restore
dotnet build Test/Lan.Shapes.InteractionProbe/Lan.Shapes.InteractionProbe.csproj --no-restore
```

首次构建需要先对相应项目执行 `dotnet restore`。本次网络还原受限，使用本机已缓存的 NuGet 包完成了还原；没有改动仓库的包源配置。

| 验证项目 | 结果 |
| --- | --- |
| `Lan.SketchBoard.Tests` | 531 通过，0 失败，0 跳过 |
| `Lan.Shapes.Tests` | 9 通过，0 失败，0 跳过 |
| WPF 测试应用 | 构建成功 |
| 原生捕获验证程序 | 构建成功；四个交互检查通过 |

此次 ShapeVisualBase 修复新增 97 个主套件用例，分布在 `BuiltinShapeContractTests`、`CustomShapeContractTests` 和 `ShapeVisualContractRegressionTests`。另外修复了原先缺少测试 SDK/适配器的 `Lan.Shapes.Tests`，将它纳入解决方案 Tests 分组，并更新其已过时的颜色配置类型。

TRX 结果保存在被 Git 忽略的 `TestResults/ShapeVisual` 目录。

## 实际 WPF 窗口验证

使用 `Test/Lan.Shapes.TestApp`，通过真实桌面鼠标输入执行以下操作：

| 操作 | 观察结果 |
| --- | --- |
| 创建矩形并松开鼠标 | 形状完成，创建句柄消失；总形状数由 2 变为 3 |
| 点击矩形 | 显示 Selected 状态和四个角句柄 |
| 平移矩形 | 正文和句柄共同移动，属性中的坐标同步更新 |
| 锁定后尝试拖动 | 显示 Locked 状态，句柄隐藏；坐标和尺寸保持不变 |
| 解锁 | 恢复已有选择及四个句柄 |
| 拖动右下句柄 | 左上角保持不变，宽高和右下坐标同步更新 |
| 视口由 36.75% 缩放到 40.43% | 模型坐标和尺寸保持不变，句柄仍位于正确的角点 |
| 从左向右完整框选 | 选中矩形，模型坐标保持不变 |
| 从右向左交叉框选 | 选中相交矩形，模型坐标保持不变 |

最终属性面板显示左上角约 `(521.3, 691.9)`、右下角约 `(1310.4, 1127.2)`、宽高约 `789.1 × 435.4`，缩放和两次框选均未改变这些值。这里记录的是面板舍入后的显示值。

## 真实捕获丢失验证

`Test/Lan.Shapes.InteractionProbe` 是可重复运行的桌面验证程序，不加入自动测试套件。它不读取或保存用户配置。

构建后从交互桌面启动 `Test/Lan.Shapes.InteractionProbe/bin/Debug/net8.0-windows/Lan.Shapes.InteractionProbe.exe`，点击 **Run capture checks**。

| 场景 | 结果 |
| --- | --- |
| 编辑已完成形状，主动释放捕获 | PASS |
| 编辑已完成形状，将捕获转移到另一控件 | PASS |
| 创建未完成形状，主动释放捕获 | PASS |
| 创建未完成形状，将捕获转移到另一控件 | PASS |

每个检查先确认画板实际获得 `Mouse.Captured`，再确认 WPF 向画板派发一次 `LostMouseCapture`。随后验证拖动标志清除、残留移动/松开不会改变几何或触发创建完成，以及新的按下可以恢复编辑或完成创建。

这些检查的板内按下、移动和松开通过受保护入口调用；捕获和丢失事件由真实 HWND 中的 WPF 产生，没有手动调用 `OnLostMouseCapture`。测试应用的手工操作另行覆盖真实鼠标路由。在本次受限命令环境中，测试宿主创建的窗口无法取得捕获，因此没有把依赖交互桌面的检查放进 CI。相关底层路径可参见 [WPF HwndMouseInputProvider 源码](https://source.dot.net/PresentationCore/System/Windows/InterOp/HwndMouseInputProvider.cs.html)。

原生窗口报告 `HWND DPI: 144 x 144`，即 150% 系统缩放，最终显示 `Result: 4/4 passed`。不同 DPI 显示器之间的跨屏拖动没有在本次桌面环境中实测。
