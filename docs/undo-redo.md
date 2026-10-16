# 撤销与重做

工具栏的“撤销”和“重做”按钮会随历史状态启用或禁用。画布、图形树和其他非文本区域支持：

- Ctrl+Z：撤销。
- Ctrl+Y 或 Ctrl+Shift+Z：重做。

输入框内 Ctrl+Z 保留文本编辑器自己的撤销行为。

## 支持范围

支持单个或批量删除、清空图形、单图形移动、组合移动，以及图形已有手柄提供的旋转、尺寸、顶点和厚度调整。属性面板中一次有效的几何提交也记为一步。一次鼠标拖动只记一步；几何未变化或拖回原状态不记录，也不会清空已有重做分支。批量删除一次撤销全部恢复。删除恢复保留相同图形实例、原叠放顺序、组合对象及符合当前显示状态的原选择。几何恢复精确的模型参数和附加文字位置，保持样式、标签、对象身份及独立 Transform。

Esc、鼠标捕获丢失、画板卸载或更换仓库会停止拖动，已发生的编辑保留为一个可撤销步骤。锁定、选择、鼠标悬停、图层显示隐藏、视口缩放和平移不产生历史步骤。历史恢复可以作用于已锁定或已隐藏的图形，但不会改变其锁定或隐藏状态。DXF 在松开鼠标时合并变换后，撤销／重做同时恢复显示与导出状态。

每个画板独立保存最近 100 步历史，仅驻留内存。撤销后发生新编辑会丢弃重做分支。新增图形、组合／解组、重新分配图层等尚未接入历史的模型编辑会清空历史，防止旧快照覆盖之后的操作。外部代码直接修改几何参数时应使用下述编辑范围；没有编辑范围的几何属性修改继续清空历史。组整体旋转和缩放不属于本次支持范围。

## 扩展接口

IShapeHistory 是可选接口，与 IShapeRepository 分开，SketchBoardDataManager 实现两者。

```csharp
if (repository is IShapeHistory history)
{
    using (history.BeginHistoryTransaction())
    {
        repository.RemoveShape(first);
        repository.RemoveShape(second);
    }
    if (history.CanUndo) history.Undo();
    if (history.CanRedo) history.Redo();
}
```

事务允许嵌套，以最外层 Dispose 为提交边界。事务期间撤销／重做不可用。RemoveShapes、RemoveAt(index, count)、ClearAllShapes 和 TranslateShapes 的批量操作已自动合并。外部载入或替换整份图形数据之后，可以调用 ClearHistory 显式建立新的历史起点。

自定义可移动图形通过覆写 CaptureTranslationStateCore 和 RestoreTranslationStateCore 提供不可变模型状态，恢复时应直接写回精确坐标并更新几何，避免反向位移的浮点误差。基类负责保存与恢复 AddText 附加文字、刷新句柄和合并绘制。没有适配状态捕获的第三方图形仍可移动，但移动会清空历史。自定义几何修改应发送 PropertyChanged 通知，使未记录的修改能够使旧历史失效。

旋转和 resize 使用独立的完整几何快照 CaptureGeometryStateCore／RestoreGeometryStateCore，必须包含手柄或属性编辑会改变的全部模型参数。自定义图形应覆写完整几何快照；基类默认不提供此能力，避免遗漏尺寸或角度。调用 BeginGeometryEdit 将一组修改合并为一步，范围允许嵌套，外层结束时比较前后状态；范围内的 Translate 不另记一步。画板已经为单图形鼠标编辑自动开启范围，属性面板也已自动接入。

```csharp
using (circle.BeginGeometryEdit())
{
    circle.SetCircle(new Point(100, 120), 40);
}
```

编辑范围应覆盖操作的全部模型更新，包括自定义图形在鼠标释放时合并变换的更新。它负责历史记录，没有异常回滚语义。

历史不保存完整文档，也不提供外部事件处理器或自定义绘制抛异常时的通用事务回滚；重放失败会清空历史，避免继续使用可能失效的记录。

## 验证

```powershell
dotnet test Test/Lan.SketchBoard.Tests/Lan.SketchBoard.Tests.csproj --no-restore
```
