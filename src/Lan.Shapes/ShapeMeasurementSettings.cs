using System;

namespace Lan.Shapes
{
    /// <summary>
    /// Global measurement calibration shared by every shape layer.
    /// </summary>
    public class ShapeMeasurementSettings
    {
        public double PixelPerUnit { get; set; } = 1;
        public int UnitsPerMillimeter { get; set; } = 1;
        public string UnitName { get; set; } = "px";

        public void Validate()
        {
            if (double.IsNaN(PixelPerUnit) ||
                double.IsInfinity(PixelPerUnit) ||
                PixelPerUnit <= 0)
            {
                throw new InvalidOperationException(
                    $"{nameof(PixelPerUnit)} must be greater than zero.");
            }

            if (UnitsPerMillimeter <= 0)
            {
                throw new InvalidOperationException(
                    $"{nameof(UnitsPerMillimeter)} must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(UnitName))
            {
                throw new InvalidOperationException(
                    $"{nameof(UnitName)} must not be empty.");
            }
        }
    }
}
