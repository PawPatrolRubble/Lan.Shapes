# IImageViewerViewModel IoC 使用说明

本文档说明如何通过 IoC（控制反转）容器获取和使用 `IImageViewerViewModel` 实例。

## 接口定义

`IImageViewerViewModel` 定义了图像查看器视图模型的核心契约，位于 `Lan.ImageViewer` 命名空间：

```csharp
public interface IImageViewerViewModel
{
    ISketchBoardDataManager SketchBoardDataManager { get; }
    ObservableCollection<GeometryType> GeometryTypeList { get; }
    GeometryType SelectedGeometryType { get; }
    ImageSource Image { get; set; }
    double Scale { get; set; }
    ObservableCollection<ShapeLayer> Layers { get; set; }
    ShapeLayer SelectedShapeLayer { get; set; }
    Point MouseDoubleClickPosition { get; set; }
    ICommand ZoomOutCommand { get; }
    ICommand ZoomInCommand { get; }
    ICommand ScaleToOriginalSizeCommand { get; }
    ICommand ScaleToFitCommand { get; }
    ICommand DeleteShapeCommand { get; }
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
    containerRegistry.Register<IImageViewerViewModel, ImageViewerControlViewModel>();
    containerRegistry.Register<ISketchBoardDataManager, SketchBoardDataManager>();
}
```

注册为 **transient** 生命周期（每次解析返回新实例）。

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

---

## 方案二：MSDI（`Lan.Shapes.TestApp`）

### 1. 注册服务

在 `App.xaml.cs` 中通过 `ServiceCollection` 注册：

```csharp
private void ConfigServices()
{
    _serviceCollection.AddSingleton<MainWindowViewModel>();
    _serviceCollection.AddSingleton<MainWindow>();
    _serviceCollection.AddSingleton<IGeometryTypeManager, GeometryTypeManager>();
    _serviceCollection.AddSingleton<IShapeLayerManager, ShapeLayerManager>();
    _serviceCollection.AddTransient<IImageViewerViewModel, ImageViewerControlViewModel>();
    _serviceCollection.AddTransient<ISketchBoardDataManager, SketchBoardDataManager>();

    ServiceProvider = _serviceCollection.BuildServiceProvider();
}
```

同样注册为 **transient** 生命周期。

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

与方案一相同：

```xml
<Window xmlns:imageViewer="clr-namespace:Lan.ImageViewer;assembly=Lan.ImageViewer">
    <imageViewer:ImageViewerControl
        DataContext="{Binding Camera1}" />
</Window>
```

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

## 关键注意事项

1. **生命周期默认为 transient**：每次调用 `Resolve` / `GetService` 都会创建新实例。如果需要跨请求共享状态，请改为 `RegisterSingleton`。
2. **不直接 new**：避免 `new ImageViewerControlViewModel()`，应始终通过 IoC 容器获取实例。
3. **构造函数依赖**：`ImageViewerControlViewModel` 的构造函数需要 `IShapeLayerManager`、`ISketchBoardDataManager`、`IGeometryTypeManager`，这些依赖也必须在 IoC 中注册。
4. **同一 AppDomain 内两种容器不互通**：DryIoc 和 MSDI 注册的服务互不影响，需各自独立配置。
