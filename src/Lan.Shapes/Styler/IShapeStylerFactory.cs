#nullable enable
using System.Windows.Media;

namespace Lan.Shapes.Styler
{
    /// <summary>
    /// Abstracts creation of shape stylers, enabling substitution for testing, theming,
    /// or alternative styling strategies without modifying layer construction.
    /// </summary>
    public interface IShapeStylerFactory
    {
        /// <summary>Builds a styler from a configuration parameter (layer StyleSchema).</summary>
        IShapeStyler CreateStyler(ShapeStylerParameter parameter);

        IShapeStyler ShapeUnselectedVisualState();
        IShapeStyler ShapeSelectedVisualState();
        IShapeStyler DottedLineStyler();
        IShapeStyler CustomShapeStyler(Brush fillColor, Brush strokeColor, double strokeThickness);
        IShapeStyler CustomShapeStyler(Brush fillColor, Brush strokeColor, double strokeThickness, double dragHandleSize);
    }
}
