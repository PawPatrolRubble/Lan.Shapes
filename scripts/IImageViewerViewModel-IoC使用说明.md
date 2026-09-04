# IImageViewerViewModel IoC 使用说明

本文档说明如何通过 IoC（控制反转）容器获取和使用 `IImageViewerViewModel` 实例。

## 接口定义

`IImageViewerViewModel` 定义了图像查看器视图模型的核心契约，位于 `Lan.ImageViewer` 命名空间。

**依赖分层（WPF-only ISP）：**

| 成员 | 用途 |
|------|------|
| `SketchBoardDataManager` | 仅给 WPF 控件 DP 绑定 / 视觉宿主（`VisualCollection`、缩放反馈） |
| `ShapeRepository` | 形状列表、选择、CRUD、事件（VM / 服务 / 测试优先使用） |
| `Shapes` / `SelectedShape` | XAML 列表绑定；`SelectedShape` 对应 `SelectedGeometry`，**不是** 正在绘制的 `CurrentGeometryInEdit` |

```csharp
public interface IImageViewerViewModel
{
    // Control host only — do not use for shape logic in new code
    ISketchBoardDataManager SketchBoardDataManager { get; }

    // Preferred shape-data surface
    IShapeRepository ShapeRepository { get; }
    ObservableCollection<ShapeVisualBase> Shapes { get; }
    ShapeVisualBase? SelectedShape { get; set; }

    ObservableCollection<GeometryType> GeometryTypeList { get; }
    GeometryType? SelectedGeometryType { get; }
    ImageSource Image { get; set; }
    double Scale { get; set; }
    ObservableCollection<ShapeLayer> Layers { get; set; }
    ShapeLayer SelectedShapeLayer { get; set; }
    Point MouseDoubleClickPosition { get; set; }
    ICommand ZoomOutCommand { get; }
    ICommand ZoomInCommand { get; }
    ICommand ScaleToOriginalSizeCommand { get; }
    ICommand ScaleToFitCommand { get; }
    ICommand DeleteShapeCommand { get; }  // deletes SelectedShape
    bool ShowSimpleCanvas { get; set; }
    bool ShowShapeTypes { get; set; }
    void FilterGeometryTypes(Expression<Func<GeometryType, bool>> predicate);
}
```

## 项目中的两种 IoC 方案

本项目在两个不同宿主应用中分别使用了两种 IoC 容器：

| 宿主应用 | IoC 容器 | 基类 |
|----------|----------|------|
| `Lan.Shapes.SimpleApp` | DryIoc（通过 `Prism.DryIoc`） | `PrismApplication` |
| `Lan.Shapes.TestApp` | `Microsoft.Extensions.DependencyInjection` | 标准 `Application` |

---

## 方案一：Prism + DryIoc（`Lan.Shapes.SimpleApp`）

### 1. 注册服务

在 `ImageViewerModule` 中注册接口与实现的映射：

**文件：** `src/Lan.ImageViewer.Prism/ImageViewerModule.cs`

```csharp
public void RegisterTypes(IContainerRegistry containerRegistry)
{
    containerRegistry.RegisterSingleton<IGeometryTypeManager, GeometryTypeManager>();
    containerRegistry.RegisterSingleton<IShapeLayerManager, ShapeLayerManager>();
    containerRegistry.RegisterSingleton<IGeometryIconProvider, ResourceDictionaryGeometryIconProvider>();
    containerRegistry.RegisterSingleton<IShapeStylerFactory, ShapeStylerFactory>();
    containerRegistry.Register<IImageViewerViewModel, ImageViewerControlViewModel>();

    // Fat manager for WPF controls; same instance also as IShapeRepository
    containerRegistry.Register<ISketchBoardDataManager, SketchBoardDataManager>();
    containerRegistry.Register<IShapeRepository>(c => c.Resolve<ISketchBoardDataManager>());
}
```

| 接口 | 角色 |
|------|------|
| `IGeometryIconProvider` | 调色板图标（`Geometries.xaml` / 自定义主题） |
| `IShapeStylerFactory` | 层样式构建；`ShapeLayerManager` 构造层时注入 |
| `IShapeRepository` | 与 fat manager **同一实例**，形状状态用 |

`OnInitialized` 从 `LanShapesConfig.json` 的 `AvailableGeometryTypes` 读取调色板类型名并注册。缺省该键时注册完整 catalog；未知类型名启动失败。


注册为 **transient** 生命周期（每次解析返回新实例）的是 VM / board manager；图标与 styler factory 为 **singleton**。

### 2. 加载模块

在宿主应用的 `App.xaml.cs` 中加载模块：

```csharp
protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
{
    base.ConfigureModuleCatalog(moduleCatalog);
    moduleCatalog.AddModule<ImageViewerModule>();
}
```

### 3. 解析服务

通过 `ContainerLocator.Container.Resolve<T>()` 获取实例：

**在 ViewModel 中使用：**

```csharp
// MainPageViewModel.cs
Camera1 = ContainerLocator.Container.Resolve<IImageViewerViewModel>();
```

**在 Code-Behind 中设置 DataContext：**

```csharp
// MainPage.xaml.cs
DataContext = ContainerLocator.Container.Resolve<MainPageViewModel>();
```

**完整示例 — 在 ViewModel 中创建多个图像查看器：**

```csharp
public class MainPageViewModel : BindableBase
{
    public IImageViewerViewModel Camera1 { get; set; }
    public IImageViewerViewModel Camera2 { get; set; }

    public MainPageViewModel()
    {
        Camera1 = ContainerLocator.Container.Resolve<IImageViewerViewModel>();
        Camera2 = ContainerLocator.Container.Resolve<IImageViewerViewModel>();
        // Camera1 和 Camera2 是两个独立实例（transient）
    }
}
```

### 4. XAML 绑定

在 XAML 中通过父 ViewModel 的 DataContext 绑定：

```xml
<Window xmlns:imageViewer="clr-namespace:Lan.ImageViewer;assembly=Lan.ImageViewer">
    <imageViewer:ImageViewerControl
        DataContext="{Binding Camera1}" />
</Window>
```

列表选择应绑定到 VM 的 `SelectedShape`（不要绑 `CurrentGeometryInEdit`）：

```xml
<ListBox
    ItemsSource="{Binding Shapes}"
    SelectedItem="{Binding SelectedShape, Mode=TwoWay}" />
```

控件内部的 `ImageViewer` 仍绑定 fat manager：

```xml
SketchBoardDataManager="{Binding SketchBoardDataManager}"
```

---

## 方案二：MSDI（`Lan.Shapes.TestApp`）

### 1. 注册服务

在 `App.xaml.cs` 中通过 `ServiceCollection` 注册：

```csharp
private void ConfigServices()
{
    _serviceCollection.AddSingleton<MainWindowViewModel>();
    _serviceCollection.AddSingleton<MainWindow>();
    _serviceCollection.AddSingleton<IShapeLayerManager, ShapeLayerManager>();
    _serviceCollection.AddSingleton<IGeometryTypeManager>(geometryTypeManager);
    _serviceCollection.AddSingleton<IGeometryIconProvider, ResourceDictionaryGeometryIconProvider>();
    _serviceCollection.AddSingleton<IShapeStylerFactory, ShapeStylerFactory>();
    _serviceCollection.AddTransient<IImageViewerViewModel, ImageViewerControlViewModel>();
    _serviceCollection.AddTransient<ISketchBoardDataManager, SketchBoardDataManager>();
    _serviceCollection.AddTransient<IShapeRepository>(
        sp => sp.GetRequiredService<ISketchBoardDataManager>());

    ServiceProvider = _serviceCollection.BuildServiceProvider();
}
```

同样：VM / board manager **transient**；图标与 styler factory **singleton**。

### 2. 暴露 ServiceProvider

在 `App` 类中暴露静态属性供全局访问：

```csharp
public partial class App : Application
{
    private readonly ServiceCollection _serviceCollection = new ServiceCollection();
    public static IServiceProvider ServiceProvider { get; private set; }
    // ...
}
```

### 3. 解析服务

**方式一：在构造函数中注入 `IServiceProvider`**

```csharp
public class MainWindowViewModel
{
    public IImageViewerViewModel Camera1 { get; set; }

    public MainWindowViewModel(
        IServiceProvider serviceProvider,
        IShapeLayerManager shapeLayerManager)
    {
        Camera1 = serviceProvider.GetService<IImageViewerViewModel>();
    }
}
```

**方式二：通过静态 `App.ServiceProvider` 直接解析**

```csharp
// MainWindow.xaml.cs
DataContext = App.ServiceProvider.GetRequiredService<MainWindowViewModel>();
```

### 4. XAML 绑定

与方案一相同。

---

## XAML 设计时 DataContext

两个方案的 ImageViewerControl 都通过设计时属性声明 DataContext 类型，方便 XAML 设计器提供智能提示：

```xml
<!-- ImageViewerControl.xaml -->
<UserControl
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    d:DataContext="{d:DesignInstance local:IImageViewerViewModel}">
```

---

## 扩展点（Phase 5）

### 调色板图标

- 默认：`ResourceDictionaryGeometryIconProvider` 读 `Lan.ImageViewer/Geometries.xaml`
- 替换：注册自己的 `IGeometryIconProvider`（测试可注入 stub）
- 新增形状图标：在 `Geometries.xaml` 加资源键，或在 provider 里加 type→key 别名；**不要**改 VM 字典

```csharp
// VM 构造（可选注入，缺省用 ResourceDictionary provider）
public ImageViewerControlViewModel(
    IShapeLayerManager shapeLayerManager,
    ISketchBoardDataManager sketchBoardDataManager,
    IGeometryTypeManager geometryTypeManager,
    IGeometryIconProvider? geometryIconProvider = null)
```

### 层样式工厂

- `ShapeLayer` / `ShapeLayerManager` 通过 `IShapeStylerFactory` 构建 styler
- 测试可注入 counting / themed factory

### 画板上下文

- 仅板尺寸相关形状实现 `IBoardContextAware`（当前：`FixedCenterCircle`）
- `CreateNewGeometry` 在宿主为 `SketchBoard` 时调用 `OnBoardContextAvailable`
- 中途取消：`ShapeVisualBase.OnShapeCreationCancelled()` → manager `RemoveShape`

---

## 关键注意事项

1. **生命周期**：VM / board manager 默认 **transient**；`IGeometryIconProvider` / `IShapeStylerFactory` / layer manager 为 **singleton**。
2. **不直接 new**：避免 `new ImageViewerControlViewModel()`，应始终通过 IoC 容器获取实例。
3. **构造函数依赖**：`IShapeLayerManager`、`ISketchBoardDataManager`、`IGeometryTypeManager`，可选 `IGeometryIconProvider`。fat manager 同时实现 `IShapeRepository`。
4. **同一 AppDomain 内两种容器不互通**：DryIoc 和 MSDI 注册的服务互不影响，需各自独立配置。
5. **选择语义**：列表 / 删除使用 `SelectedShape`（`SelectedGeometry`）。`CurrentGeometryInEdit` 仅表示正在绘制、尚未提交的几何。
6. **新服务优先依赖 `IShapeRepository`**：除非需要 `VisualCollection` / `InitializeVisualCollection` / `OnImageViewerPropertyChanged`。
