using System.Windows;

namespace Lan.Shapes.Interfaces
{
    /// <summary>A multi-click sketch that previews between clicks and commits only when complete.</summary>
    public interface IStagedSketch
    {
        void PreviewPointer(Point point);
    }
}
