using System;
using System.IO;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Styler;
using Lan.SketchBoard;
using Microsoft.Extensions.Configuration;
using Prism.Ioc;
using Prism.Modularity;

namespace Lan.ImageViewer.Prism
{
    public class ImageViewerModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var configuration = containerProvider.Resolve<IConfiguration>();
            var baseDirectory = configuration["configBaseDir"] ?? AppContext.BaseDirectory;
            var layerPath = configuration["shapeLayerPath"];

            if (!string.IsNullOrWhiteSpace(layerPath))
            {
                var fullPath = Path.Combine(baseDirectory, layerPath);
                containerProvider.Resolve<IShapeLayerManager>().ReadShapeLayers(fullPath);
            }

            var geometryTypeManager = containerProvider.Resolve<IGeometryTypeManager>();
            GeometryTypeRegistration.RegisterDefaultGeometryTypes(geometryTypeManager);
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<IGeometryTypeManager, GeometryTypeManager>();
            containerRegistry.RegisterSingleton<IShapeLayerManager, ShapeLayerManager>();
            containerRegistry.RegisterSingleton<IGeometryIconProvider, ResourceDictionaryGeometryIconProvider>();
            containerRegistry.RegisterSingleton<IShapeStylerFactory, ShapeStylerFactory>();
            containerRegistry.Register<IImageViewerViewModel, ImageViewerControlViewModel>();

            // Fat manager for WPF controls; also exposed as IShapeRepository for
            // consumers that only need shape state (VMs/services/tests).
            containerRegistry.Register<ISketchBoardDataManager, SketchBoardDataManager>();
            containerRegistry.Register<IShapeRepository>(c => c.Resolve<ISketchBoardDataManager>());
        }
    }
}
