using Lan.ImageViewer;
using Lan.Shapes.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using Lan.SketchBoard;
using Microsoft.Extensions.Logging;
using Serilog;
using Lan.Shapes.App.ViewModels;
using ImageViewerControlViewModel = Lan.ImageViewer.Prism.ImageViewerControlViewModel;
using ShapeLayerManager = Lan.ImageViewer.Prism.ShapeLayerManager;
using Lan.ImageViewer.Prism;
using Lan.Shapes.Styler;

namespace Lan.Shapes.App
{
    public partial class App : Application
    {

        public static IServiceProvider ServiceProvider;
        private readonly IServiceCollection _serviceCollection = new ServiceCollection();

        protected override void OnStartup(StartupEventArgs e)
        {
            ConfigServices();
            var shapeLayerManager = ServiceProvider.GetRequiredService<IShapeLayerManager>();
            shapeLayerManager.ReadConfiguration(
                System.IO.Path.Combine(AppContext.BaseDirectory, "LanShapesConfig.json"));
            GeometryTypeRegistration.RegisterGeometryTypes(
                ServiceProvider.GetRequiredService<IGeometryTypeManager>(),
                shapeLayerManager.Configuration.AvailableGeometryTypes);
            // Setup the Serilog logger
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Debug()
                .CreateLogger();

            // Initalie the XamlFlair loggers using the LoggerFactory (with Serilog support)
            //XamlFlair.Animations.InitializeLoggers(new LoggerFactory().AddSerilog());

        }

        private void ConfigServices()
        {

            //var config = new ConfigurationBuilder()
            //    .SetBasePath(Environment.CurrentDirectory)
            //    .AddJsonFile("LanShapesConfig.json").Build();

            //_serviceCollection.AddSingleton(config);


            _serviceCollection.AddSingleton<MainWindowViewModel>();
            _serviceCollection.AddSingleton<MainWindow>();
            _serviceCollection.AddSingleton<IShapeLayerManager, ShapeLayerManager>();
            _serviceCollection.AddSingleton<IGeometryTypeManager, GeometryTypeManager>();
            _serviceCollection.AddSingleton<IGeometryIconProvider, ResourceDictionaryGeometryIconProvider>();
            _serviceCollection.AddSingleton<IShapeStylerFactory, ShapeStylerFactory>();
            _serviceCollection.AddTransient<IImageViewerViewModel, ImageViewerControlViewModel>();
            _serviceCollection.AddTransient<ISketchBoardDataManager, SketchBoardDataManager>();
            _serviceCollection.AddTransient<IShapeRepository>(
                sp => sp.GetRequiredService<ISketchBoardDataManager>());

            ServiceProvider = _serviceCollection.BuildServiceProvider();
        }
    }

    //public class Program
    //{
    //    [STAThread]
    //    public static void Main(params string[] args)
    //    {
    //        var app = new App();
    //        app.Run();
    //    }
    //}
}
