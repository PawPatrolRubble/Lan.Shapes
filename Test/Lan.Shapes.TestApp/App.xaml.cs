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
            ServiceProvider.GetService<IShapeLayerManager>().ReadShapeLayers("ShapeLayers.json");
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
            //    .AddJsonFile("ShapeLayers.json").Build();

            //_serviceCollection.AddSingleton(config);


            _serviceCollection.AddSingleton<MainWindowViewModel>();
            _serviceCollection.AddSingleton<MainWindow>();
            _serviceCollection.AddSingleton<IShapeLayerManager, ShapeLayerManager>();
            var geometryTypeManager = new GeometryTypeManager();
            GeometryTypeRegistration.RegisterDefaultGeometryTypes(geometryTypeManager);
            _serviceCollection.AddSingleton<IGeometryTypeManager>(geometryTypeManager);
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
