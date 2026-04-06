using System.Windows.Media;

namespace Lan.Shapes.Styler
{
    /// <summary>
    /// Abstracts creation of shape stylers, enabling substitution for testing, theming,
    /// or alternative styling strategies without modifying <c>SketchBoardDataManager</c>.
    /// </summary>
    public interface IShapeStylerFactory
    {
        IShapeStyler ShapeUnselectedVisualState();
        IShapeStyler ShapeSelectedVisualState();
        IShapeStyler DottedLineStyler();
        IShapeStyler CustomShapeStyler(Brush fillColor, Brush strokeColor, double strokeThickness);
        IShapeStyler CustomShapeStyler(Brush fillColor, Brush strokeColor, double strokeThickness, double dragHandleSize);
    }
}
