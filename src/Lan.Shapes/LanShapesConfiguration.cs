#nullable enable

using System;
using System.Collections.Generic;

namespace Lan.Shapes
{
    /// <summary>
    /// Root configuration for the Lan.Shapes library.
    /// </summary>
    public class LanShapesConfiguration
    {
        /// <summary>
        /// Geometry type names exposed by the sketch-tool palette.
        /// A missing value enables the full built-in catalog; an empty list disables all tools.
        /// </summary>
        public List<string>? AvailableGeometryTypes { get; set; }

        public ShapeMeasurementSettings Measurement { get; set; } = new ShapeMeasurementSettings();
        public List<ShapeLayerParameter> ShapeLayers { get; set; } = new List<ShapeLayerParameter>();

        public void Validate()
        {
            if (Measurement == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(Measurement)} configuration is required.");
            }

            Measurement.Validate();

            if (ShapeLayers == null || ShapeLayers.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{nameof(ShapeLayers)} must contain at least one layer.");
            }

            foreach (var parameter in ShapeLayers)
            {
                if (parameter == null)
                {
                    throw new InvalidOperationException(
                        $"{nameof(ShapeLayers)} must not contain null entries.");
                }

                ShapeLayer.EnsureRequiredStylerStates(
                    parameter.StyleSchema,
                    parameter.Name,
                    parameter.LayerId);
            }
        }
    }
}
