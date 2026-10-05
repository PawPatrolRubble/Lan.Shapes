# 图形组合：第一版

图形组合是画板内的逻辑关系。组合后，各成员仍是原来的图形对象，保留坐标、尺寸、样式、图层、身份和绘制顺序。`ShapeGroup` 提供运行期 `Id`、名称和只读成员列表，成员可以分别属于不同的图形类型。

## 用户操作

1. 进入选择模式，在画布或图形树中选中同一图层内至少两个已完成、未锁定、支持平移的独立图形。
2. 点击选择卡里的“组合”，或按 `Ctrl+G`。
3. 点击任意成员选中整个组合，拖动任意成员一起移动。组显示一个外接框，成员的尺寸调整手柄隐藏。
4. 点击“取消组合”，或按 `Ctrl+Shift+G`，恢复单独选择和编辑。解组不改变位置、尺寸、图层或锁定状态。

取消选中不会取消组合。`Ctrl` 点击按整组加入或移出选择，`Alt` 点击在重叠的独立图形和组合之间循环。左向右框选要求全部成员完整包含在框内，右向左框选只需命中一个成员。

双击成员会把整个组合锁定或解锁；如果调用方仅锁定其中一个成员，整组也不能移动。换图层操作应用到所有成员。隐藏图层清除选择、停止拖动，但保留组关系，重新显示后仍可选中整个组合。

删除选中组合会删除所有成员。调用方通过 `Shapes.Remove`、替换成员或直接改变单个成员的图层时，该成员所在组自动解散，其余图形保留。`Shapes.Clear` 清空所有组关系。

拖动途中取消选择、解组、移除成员、隐藏图层、锁定成员、丢失捕获、切换画板数据或绘图工具，都会停止后续组拖动更新。松开鼠标会应用最后位置；单纯点击不会移动图形。

## 数据和扩展接口

分组操作放在 `IShapeRepository`，视觉宿主仍将成员平铺在 `VisualCollection`。不会把成员重新挂到一个父 Visual，也不会合并不同成员的几何或样式。

```csharp
IShapeRepository repository = viewer.ShapeRepository;

if (repository.CanGroupShapes(repository.SelectedGeometries))
{
    var group = repository.GroupShapes(repository.SelectedGeometries, "检测区域");
    repository.TranslateShapes(group.Members, new Vector(10, 20));
}

// 提供任意组成员即可查询其组，选择也会自动展开为整组。
var selectedGroup = repository.SelectedGeometry is { } shape
    ? repository.GetGroup(shape) : null;
repository.UngroupShapes(repository.SelectedGeometries);
```

`Groups` 是只读可观察集合，`GroupsChanged` 通知组关系变化。一个图形至多属于一个组，不支持嵌套；已组合的图形需要先解组再重新组合。组合要求同一画板、同一可见图层、成员可平移，并且 Visual 变换有限且可逆。

`TranslateShapes` 接受画板坐标系中的位移，展开全部成员，先验证所有成员，再通过每个成员的 Visual 逆变换计算局部位移。缩放或旋转过的成员仍获得相同的画板位移。`ShapeVisualBase.Translate` 接受局部模型位移，并保留原有 `Visual.Transform`。

自定义图形通过下面的契约加入可移动组：

```csharp
public override bool CanTranslate => true;

protected override void TranslateCore(Vector delta)
{
    // 更新真正的模型坐标，并让这些坐标更新对应的绘制几何。
    // 例如：Center += delta;
}
```

基类统一验证有限位移、已完成和未锁定状态，移动 `AddText` 的位置、刷新句柄并合并绘制请求。扩展实现需要同步可导出坐标与绘制几何；动态改变平移能力时应发送 `CanTranslate` 属性通知。已知约束失败会在任何成员移动前拒绝请求；扩展实现自行抛出的异常不提供模型状态快照回滚。

内置图形以及粗线、箭头线、粗矩形、粗圆、粗十字、Fiber、文字、网格和 DXF 图形已适配。固定中心圆和覆盖整幅图像的标尺十字保留其固定参考语义，不能加入可移动组。Fiber 遵守 `EnableTranslation`，未适配的第三方图形默认不支持组平移。

自定义 `IShapeRepository` 实现需要补充组集合、事件及上述操作；自定义 `IImageViewerViewModel` 实现需要补充组命令、选中组摘要和可用性属性。图形基类没有新增必须实现的抽象成员。

## 第一版范围

组关系在当前画板运行期间保留。现有图层 JSON 保存只保存配置，不保存图形或组关系。完整文档保存、嵌套组、整体旋转和缩放、组移动吸附、撤销重做留待后续版本。

## 验证

新增测试位于 `ShapeGroupRepositoryTests`、`ShapeGroupInteractionTests`、`ShapeGroupViewerTests` 和 `ShapeTranslationTests`，覆盖分组约束、成员生命周期、整组选择和移动、坐标变换、取消拖动、锁定/隐藏、界面命令、模型导出、附加文字和渲染合并。

```powershell
dotnet test Test/Lan.SketchBoard.Tests/Lan.SketchBoard.Tests.csproj --no-restore --logger "trx;LogFileName=group-main.trx" --results-directory TestResults/ShapeGroup
dotnet test src/Lan.Shapes.Tests/Lan.Shapes.Tests.csproj --no-restore --logger "trx;LogFileName=group-data.trx" --results-directory TestResults/ShapeGroup
dotnet build Lan.Shapes.SimpleApp/Lan.Shapes.SimpleApp.csproj --no-restore
dotnet build Test/Lan.Shapes.TestApp/Lan.Shapes.App.csproj --no-restore
```

WPF 控件测试会实际布局 `ImageViewerControl`，验证按钮命令、可用状态和组合摘要，并把渲染截图写入 `TestResults/ShapeGroup/group-viewer.png`。交互测试使用真实 WPF 图形和画板的指针处理入口；这些自动测试不等同于真实桌面鼠标和捕获操作的手工验证。TRX 和截图目录被 Git 忽略。

2026-10-05 验证结果：

| 检查 | 结果 |
| --- | --- |
| 主 WPF 测试套件 | 634 通过，0 失败，0 跳过；包含新增 103 个组/平移案例 |
| 图形数据测试套件 | 9 通过，0 失败，0 跳过 |
| Prism 示例应用 | 构建成功，0 警告，0 错误 |
| MSDI 示例应用 | 构建成功，0 警告，0 错误 |
| 控件界面 | 已实际渲染并查看按钮、组摘要和提示布局 |
