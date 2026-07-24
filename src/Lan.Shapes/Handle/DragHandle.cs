#nullable enable
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace Lan.Shapes.Handle
{
    [DebuggerDisplay("{GeometryCenter}")]
    public abstract class DragHandle
    {
        #region constructor


        protected DragHandle(
            Size handleSize,
            Point geometryCenter,
            double detectionRange,
            int id,
            DragLocation? cursorLocation = null)
        {
            _handleSize = handleSize;  // Use backing field to avoid triggering setter before geometry is ready
            _baseHandleSize = GetReferenceHandleSize(handleSize);
            _baseDetectionRange = Math.Max(0, detectionRange);
            DetectionRange = _baseDetectionRange;
            Id = id;
            CursorLocation = cursorLocation;
            GeometryCenter = geometryCenter;  // This triggers SetCenter after size is set
        }

        #endregion

        #region private fields

        private Point _geometryCenter;

        #endregion

        #region properties

        public int Id { get; }

        public DragLocation? CursorLocation { get; }

        private readonly double _baseHandleSize;
        private readonly double _baseDetectionRange;

        public double DetectionRange { get; private set; }

        private Size _handleSize;
        public Size HandleSize
        {
            get => _handleSize;
            set
            {
                _handleSize = value;
                var handleSize = GetReferenceHandleSize(value);
                DetectionRange = _baseHandleSize > 0
                    ? _baseDetectionRange * handleSize / _baseHandleSize
                    : _baseDetectionRange;
                // Update geometry when size changes
                if (HandleGeometry != null)
                {
                    SetCenter(_geometryCenter);
                }
            }
        }

        public Point GeometryCenter
        {
            get { return _geometryCenter; }
            set
            {
                _geometryCenter = value;
                if (HandleGeometry != null)
                {
                    SetCenter(_geometryCenter);
                }
            }
        }

        public abstract Geometry? HandleGeometry { get; }

        #endregion

        #region other members

        protected abstract void SetCenter(Point center);

        public abstract bool FillContains(Point checkPoint);

        private static double GetReferenceHandleSize(Size size)
        {
            return Math.Sqrt(Math.Max(0, size.Width) * Math.Max(0, size.Height));
        }

        #endregion
    }
}
