using Lan.ImageViewer;
using Lan.Shapes.Custom;
using Lan.Shapes.DialogGeometry;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Shapes;

namespace Lan.ImageViewer.Prism
{
    /// <summary>
    /// Registers the geometry set shared by the Prism host and the sample WPF hosts.
    /// Host-specific extensions can register additional types after this call.
    /// </summary>
    public static class GeometryTypeRegistration
    {
        public static void RegisterDefaultGeometryTypes(IGeometryTypeManager geometryTypeManager)
        {
            if (geometryTypeManager == null)
            {
                throw new System.ArgumentNullException(nameof(geometryTypeManager));
            }

            geometryTypeManager.RegisterGeometryType<GridGeometry>();
            geometryTypeManager.RegisterGeometryType<GriddedRectangle>();
            geometryTypeManager.RegisterGeometryType<ThickenedCircle>();
            geometryTypeManager.RegisterGeometryType<ThickenedCross>();
            geometryTypeManager.RegisterGeometryType<ThickenedRectangle>();
            geometryTypeManager.RegisterGeometryType<ThickenedLine>();
            geometryTypeManager.RegisterGeometryType<ArrowedLine>();
            geometryTypeManager.RegisterGeometryType<Circle>();
            geometryTypeManager.RegisterGeometryType<FixedCenterCircle>();
            geometryTypeManager.RegisterGeometryType<Cross>();
            geometryTypeManager.RegisterGeometryType<Line>();
            geometryTypeManager.RegisterGeometryType<Rectangle>();
            geometryTypeManager.RegisterGeometryType<Rectangle2>();
            geometryTypeManager.RegisterGeometryType<Fiber>();
            geometryTypeManager.RegisterGeometryType<DxfGeometry>();
        }
    }
}
