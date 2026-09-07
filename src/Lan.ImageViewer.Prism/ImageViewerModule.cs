using System;
using System.IO;
using System.Linq;
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
            var lanShapesConfigPath =
                configuration["lanShapesConfigPath"] ??
                configuration["shapeLayerPath"];

            var shapeLayerManager = containerProvider.Resolve<IShapeLayerManager>();
            if (!string.IsNullOrWhiteSpace(lanShapesConfigPath))
            {
                var fullPath = ResolveLatestJsonFile(baseDirectory, lanShapesConfigPath);
                shapeLayerManager.ReadConfiguration(fullPath);
            }

            var geometryTypeManager = containerProvider.Resolve<IGeometryTypeManager>();
            GeometryTypeRegistration.RegisterGeometryTypes(
                geometryTypeManager,
                shapeLayerManager.Configuration.AvailableGeometryTypes);

        }

        /// <summary>
        /// Resolves the newest JSON config under <paramref name="baseDirectory"/>.
        /// A directory path picks the newest <c>*.json</c>; a file path picks the newest
        /// sibling matching the file-name prefix (e.g. <c>LanShapesConfig*.json</c>).
        /// </summary>
        public static string ResolveLatestJsonFile(string baseDirectory, string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return configuredPath;
            }

            var fullPath = Path.Combine(baseDirectory, configuredPath);
            string searchDirectory;
            string searchPattern;

            if (Directory.Exists(fullPath))
            {
                searchDirectory = fullPath;
                searchPattern = "*.json";
            }
            else
            {
                searchDirectory = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrEmpty(searchDirectory))
                {
                    searchDirectory = baseDirectory;
                }

                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
                searchPattern = string.IsNullOrEmpty(fileNameWithoutExtension)
                    ? "*.json"
                    : fileNameWithoutExtension + "*.json";
            }

            if (!Directory.Exists(searchDirectory))
            {
                return fullPath;
            }

            var latest = new DirectoryInfo(searchDirectory)
                .EnumerateFiles(searchPattern, SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            return latest?.FullName ?? fullPath;
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
