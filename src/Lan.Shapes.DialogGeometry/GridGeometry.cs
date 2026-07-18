using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes.DialogGeometry.Dialog;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;

namespace Lan.Shapes.DialogGeometry
{
    public class GridGeometry : ShapeVisualBase, IDataExport<GridGeometryData>
    {
        #region fields

        private readonly RectangleGeometry _boundGeometry = new RectangleGeometry();
        private Point _topLeft;
        private Point _bottomRight;
        private LineGeometry[,]? _lines;

        #endregion

        #region constructors

        public GridGeometry(ShapeLayer layer) : base(layer)
        {
            RenderGeometryGroup.Children.Add(_boundGeometry);
        }

        #endregion

        #region properties

        public Point TopLeft
        {
            get => _topLeft;
            set
            {
                _topLeft = value;
                OnTopLeftPointChanges(_topLeft);
            }
        }

        private void OnTopLeftPointChanges(Point topLeft)
        {
            _boundGeometry.Rect = new Rect(
                topLeft,
                (BottomRight.X == 0 && BottomRight.Y == 0) ? topLeft : BottomRight);
            UpdateVisual();
        }

        public Point BottomRight
        {
            get => _bottomRight;
            set
            {
                _bottomRight = value;
                OnBottomRightChanges(_bottomRight);
            }
        }

        private void OnBottomRightChanges(Point bottomRight)
        {
            _boundGeometry.Rect = new Rect(TopLeft, bottomRight);
            UpdateVisual();
        }

        public int RowCount { get; set; } = 1;
        public int ColumnCount { get; set; } = 1;

        public int RowGap { get; set; }
        public int ColumnGap { get; set; }

        public override Rect BoundsRect => _boundGeometry.Bounds;

        #endregion

        #region interface implementations

        public void FromData(GridGeometryData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            RowCount = Math.Max(1, data.RowCount);
            ColumnCount = Math.Max(1, data.ColumnCount);
            _topLeft = data.TopLeft;
            _bottomRight = data.BottomRight;
            Tag = data.Tag;

            _boundGeometry.Rect = new Rect(TopLeft, BottomRight);
            RebuildGapsFromBounds();
            RebuildLineGeometries();
            IsGeometryRendered = true;
            UpdateVisual();
        }

        public GridGeometryData GetMetaData()
        {
            return new GridGeometryData
            {
                TopLeft = TopLeft,
                BottomRight = BottomRight,
                RowCount = RowCount,
                ColumnCount = ColumnCount,
                StrokeThickness = ShapeStyler?.SketchPen.Thickness ?? 1,
                Tag = Tag
            };
        }

        #endregion

        #region interaction

        protected override void CreateHandles()
        {
        }

        protected override void HandleResizing(Point point)
        {
        }

        protected override void HandleTranslate(Point newPoint)
        {
        }

        public override void OnDeselected()
        {
        }

        public override void OnSelected()
        {
        }

        public override void OnMouseLeftButtonDown(Point mousePoint)
        {
            base.OnMouseLeftButtonDown(mousePoint);
            if (!IsGeometryRendered)
            {
                TopLeft = mousePoint;
            }
        }

        public override void OnMouseMove(Point point, MouseButtonState buttonState)
        {
            if (!IsGeometryRendered)
            {
                BottomRight = point;
            }
        }

        public override void OnMouseLeftButtonUp(Point newPoint)
        {
            if (!IsGeometryRendered)
            {
                var dialog = new DialogService();
                dialog.ShowDialog<GridDialog, GridDialogDialogViewModel>(() => new GridDialogDialogViewModel(), x =>
                {
                    if (x.Result == DialogResult.Ok)
                    {
                        RowCount = Math.Max(1, x.RowCount);
                        ColumnCount = Math.Max(1, x.ColCount);
                        BottomRight = newPoint;
                        _boundGeometry.Rect = new Rect(TopLeft, BottomRight);
                        RebuildGapsFromBounds();
                        RebuildLineGeometries();
                        IsGeometryRendered = true;
                    }
                });
            }

            UpdateVisual();
        }

        private void RebuildGapsFromBounds()
        {
            var height = BottomRight.Y - TopLeft.Y;
            var width = BottomRight.X - TopLeft.X;
            RowGap = RowCount > 0 ? (int)(height / RowCount) : 0;
            ColumnGap = ColumnCount > 0 ? (int)(width / ColumnCount) : 0;
        }

        private void RebuildLineGeometries()
        {
            if (_lines != null)
            {
                for (var rowIndex = 0; rowIndex < _lines.GetLength(0); rowIndex++)
                {
                    for (var colIndex = 0; colIndex < _lines.GetLength(1); colIndex++)
                    {
                        var line = _lines[rowIndex, colIndex];
                        if (line != null)
                        {
                            RenderGeometryGroup.Children.Remove(line);
                        }
                    }
                }
            }

            _lines = new LineGeometry[RowCount, ColumnCount];

            for (var rowIndex = 0; rowIndex < RowCount; rowIndex++)
            {
                for (var colIndex = 0; colIndex < ColumnCount; colIndex++)
                {
                    var topLeft = TopLeft + new Vector(colIndex * ColumnGap, rowIndex * RowGap);
                    var line = new LineGeometry
                    {
                        StartPoint = topLeft,
                        EndPoint = topLeft + new Vector(ColumnGap, RowGap)
                    };
                    _lines[rowIndex, colIndex] = line;
                    RenderGeometryGroup.Children.Add(line);
                }
            }
        }

        #endregion
    }
}
