namespace Lan.Shapes.Interfaces
{
    /// <summary>
    /// Marker interface for geometry metadata DTOs used to serialize and deserialize shape data.
    /// Implementations carry the minimal state required to reconstruct a shape from persistent storage.
    /// </summary>
    public interface IGeometryMetaData
    {
        double StrokeThickness { get; }

    }
}