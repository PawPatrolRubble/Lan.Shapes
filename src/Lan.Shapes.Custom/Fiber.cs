using System;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Globalization;
using Lan.Shapes.Enums;
using Lan.Shapes.Styler;
using Lan.Shapes.Handle;
using Lan.Shapes.Interfaces;
using Vector = System.Windows.Vector;

namespace Lan.Shapes.Custom
{
    public static class LineHelper
    {
        #region properties        
        public static double Length(Point start, Point end)
        {
            return Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
        }

        public static double Angle(Point start, Point end)
        {
            return Math.Atan2(end.Y - start.Y, end.X - start.X);
        }

        #endregion

        #region other members

        public static bool TryGetIntersection(
            Vector2 p1, Vector2 p2,
            Vector2 p3, Vector2 p4,
            out Vector2 intersection)
        {
            intersection = default;

            float x1 = p1.X, y1 = p1.Y;
            float x2 = p2.X, y2 = p2.Y;
            float x3 = p3.X, y3 = p3.Y;
            float x4 = p4.X, y4 = p4.Y;

            var denominator = (x1 - x2) * (y3 - y4) -
                              (y1 - y2) * (x3 - x4);

            if (Math.Abs(denominator) < 1e-6)
            {
                // Lines are parallel or coincident
                return false;
            }

            var pre = x1 * y2 - y1 * x2;
            var post = x3 * y4 - y3 * x4;

            var x = (pre * (x3 - x4) - (x1 - x2) * post) / denominator;
            var y = (pre * (y3 - y4) - (y1 - y2) * post) / denominator;

            intersection = new Vector2(x, y);
            return true;
        }

        public static Point GetIntersectionWithLine(Point start, Point end, Point lineStart, Point lineEnd)
        {
            TryGetIntersection(new Vector2((float)start.X, (float)start.Y), new Vector2((float)end.X, (float)end.Y),
                new Vector2((float)lineStart.X, (float)lineStart.Y),
                new Vector2((float)lineEnd.X, (float)lineEnd.Y), out var intersection);
            return new Point(intersection.X, intersection.Y);
        }

        public static double GetAngleBetweenLines(Point start, Point end)
        {
            return Math.Atan2(end.Y - start.Y, end.X - start.X);
        }

        public static (Point, Point) GetPerpendicularLineThroughPoint(Point lineStart, Point lineEnd, Point point, double length = 200)
        {
            Vector direction = new Vector(lineEnd.X - lineStart.X, lineEnd.Y - lineStart.Y);
            if (direction.Length > 0)
            {
                direction.Normalize();
            }
            else
            {
                return (point, point);
            }
            Vector perpendicular = new Vector(direction.Y, -direction.X);
            perpendicular *= length / 2;
            Point start = new Point(point.X - perpendicular.X, point.Y - perpendicular.Y);
            Point end = new Point(point.X + perpendicular.X, point.Y + perpendicular.Y);
            return (start, end);
        }

        public static (Point, Point) GetParallelLineThroughPoint(Point lineStart, Point lineEnd, Point point)
        {
            Vector direction = new Vector(lineEnd.X - lineStart.X, lineEnd.Y - lineStart.Y);
            if (direction.Length < 1e-6)
            {
                return (point, point);
            }
            double originalLength = direction.Length;
            direction.Normalize();
            direction *= originalLength / 2;
            Point start = new Point(point.X - direction.X, point.Y - direction.Y);
            Point end = new Point(point.X + direction.X, point.Y + direction.Y);
            return (start, end);
        }
        #endregion
    }

    public class Fiber : CustomGeometryBase, IDataExport<FiberData>
    {
        private readonly RectDragHandle _bottomLeftHandle;
        private readonly RectDragHandle _bottomRightHandle;
        private readonly LineGeometry _centerMarkHorizontal;
        private readonly LineGeometry _centerMarkVertical;
        private readonly int _crossSize = 40;
        private readonly RectDragHandle _filletRadiusHandle;
        private readonly PathFigure _pathFigure;
        private readonly PathGeometry _pathGeometry;
        private readonly RectDragHandle _rotationHandle;
        private readonly RectDragHandle _topLeftHandle;
        private readonly RectDragHandle _topRightHandle;
        private readonly RectDragHandle _triangleLeftBaseHandle;
        private readonly RectDragHandle _triangleRightBaseHandle;
        private bool _enableTranslation = true;
        private double _fiberAngle;
        private double _filletRadius = 30.0;
        private Point _tipBottomRight;
        private double _triangleAngleInDeg = 45.0;
        private Point _rectTopLeft;
        private Point _rectTopRight;
        private Point _rectBottomLeft;
        private Point _rectBottomRight;
        private readonly EllipseGeometry _filletGeometry;
        private double _translationX;
        private double _translationY;

        public Point RectTopLeft
        {
            get => _rectTopLeft;
            set { _rectTopLeft = value; UpdateGeometry(); }
        }

        public Point RectTopRight
        {
            get => _rectTopRight;
            set { _rectTopRight = value; UpdateGeometry(); }
        }

        public Point RectBottomLeft
        {
            get => _rectBottomLeft;
            set { _rectBottomLeft = value; UpdateGeometry(); }
        }

        public Point RectBottomRight
        {
            get => _rectBottomRight;
            set { _rectBottomRight = value; UpdateGeometry(); }
        }

        public double FilletRadius
        {
            get => _filletRadius;
            set
            {
                SetField(ref _filletRadius, value, nameof(FilletRadius));
                UpdateFilletCircle();
                UpdateVisual();
            }
        }

        public double FiberAngle
        {
            get => _fiberAngle;
            set
            {
                double oldAngle = _fiberAngle;
                if (!SetField(ref _fiberAngle, value, nameof(FiberAngle))) return;
                Point center = _filletGeometry.Center;
                RotateAboutAngle(AngleToRadian(-(value - oldAngle)), center);
            }
        }

        public Point FilletCenter => _filletGeometry.Center;

        public double TriangleBottomEdgeAngleInDeg
        {
            get => _triangleAngleInDeg;
            set
            {
                SetField(ref _triangleAngleInDeg, value, nameof(TriangleBottomEdgeAngleInDeg));
                UpdateGeometry();
            }
        }

        public Point TriangleApex
        {
            get => _tipBottomRight;
            private set => SetField(ref _tipBottomRight, value, nameof(TriangleApex));
        }

        public double TranslationX
        {
            get => _translationX;
            set { SetField(ref _translationX, value, nameof(TranslationX)); TranslateShape(value, 0.0); }
        }

        public double TranslationY
        {
            get => _translationY;
            set { SetField(ref _translationY, value, nameof(TranslationY)); TranslateShape(0.0, value); }
        }

        public Fiber(ShapeLayer shapeLayer) : base(shapeLayer)
        {
            IShapeStyler styler = shapeLayer.GetStyler(ShapeVisualState.Normal);
            _topLeftHandle = RectDragHandle.CreateRectDragHandleFromStyler(styler, new Point(), 1);
            _topRightHandle = RectDragHandle.CreateRectDragHandleFromStyler(styler, new Point(), 2);
            _bottomRightHandle = RectDragHandle.CreateRectDragHandleFromStyler(styler, new Point(), 3);
            _bottomLeftHandle = RectDragHandle.CreateRectDragHandleFromStyler(styler, new Point(), 4);
            _triangleLeftBaseHandle = RectDragHandle.CreateRectDragHandleFromStyler(styler, new Point(), 7);
            _triangleRightBaseHandle = RectDragHandle.CreateRectDragHandleFromStyler(styler, new Point(), 8);
            _filletRadiusHandle = RectDragHandle.CreateRectDragHandleFromStyler(styler, new Point(), 9);
            _rotationHandle = RectDragHandle.CreateRectDragHandleFromStyler(styler, new Point(), 10);

            _pathGeometry = new PathGeometry();
            _pathFigure = new PathFigure();
            _filletGeometry = new EllipseGeometry();
            _centerMarkHorizontal = new LineGeometry();
            _centerMarkVertical = new LineGeometry();

            _pathGeometry.Figures.Add(_pathFigure);
            _pathFigure.IsClosed = false;
            _pathGeometry.FillRule = FillRule.Nonzero;

            _fiberAngle = GetFiberAngleInDeg();

            Handles.AddRange(new DragHandle[8]
            {
                _topLeftHandle, _topRightHandle, _bottomLeftHandle, _bottomRightHandle,
                _triangleLeftBaseHandle, _triangleRightBaseHandle, _filletRadiusHandle, _rotationHandle
            });

            RenderGeometryGroup.Children.Add(_pathGeometry);
            RenderGeometryGroup.Children.Add(_centerMarkHorizontal);
            RenderGeometryGroup.Children.Add(_centerMarkVertical);
            RenderGeometryGroup.Children.Add(_topLeftHandle.HandleGeometry);
            RenderGeometryGroup.Children.Add(_topRightHandle.HandleGeometry);
            RenderGeometryGroup.Children.Add(_bottomLeftHandle.HandleGeometry);
            RenderGeometryGroup.Children.Add(_bottomRightHandle.HandleGeometry);
            RenderGeometryGroup.Children.Add(_triangleLeftBaseHandle.HandleGeometry);
            RenderGeometryGroup.Children.Add(_triangleRightBaseHandle.HandleGeometry);
            RenderGeometryGroup.Children.Add(_filletRadiusHandle.HandleGeometry);
            RenderGeometryGroup.Children.Add(_rotationHandle.HandleGeometry);
            RenderGeometryGroup.Children.Add(_filletGeometry);
        }

        private void TranslateShape(double dx, double dy)
        {
            RectBottomLeft = new Point(RectBottomLeft.X + dx, RectBottomLeft.Y + dy);
            RectTopLeft = new Point(RectTopLeft.X + dx, RectTopLeft.Y + dy);
            RectBottomRight = new Point(RectBottomRight.X + dx, RectBottomRight.Y + dy);
            RectTopRight = new Point(RectTopRight.X + dx, RectTopRight.Y + dy);
        }

        public void FromData(FiberData data)
        {
            _enableTranslation = data.EnableTranslation;
            FiberAngle = data.FiberAngleInDeg;
            FilletRadius = data.FilletRadius;

            double w2 = data.Width / 2.0;
            double h2 = data.Height / 2.0;
            double a = -data.FiberAngleInDeg * Math.PI / 180.0;
            double cosA = Math.Cos(a);
            double sinA = Math.Sin(a);

            Point center = data.FilletCenter;
            Point p1 = new Point(-w2, -h2);
            Point p2 = new Point(w2, -h2);
            Point p3 = new Point(-w2, h2);
            Point p4 = new Point(w2, h2);

            RectTopLeft = new Point(center.X + p1.X * cosA - p1.Y * sinA, center.Y + p1.X * sinA + p1.Y * cosA);
            RectTopRight = new Point(center.X + p2.X * cosA - p2.Y * sinA, center.Y + p2.X * sinA + p2.Y * cosA);
            RectBottomLeft = new Point(center.X + p3.X * cosA - p3.Y * sinA, center.Y + p3.X * sinA + p3.Y * cosA);
            RectBottomRight = new Point(center.X + p4.X * cosA - p4.Y * sinA, center.Y + p4.X * sinA + p4.Y * cosA);
            
            UpdateGeometry();

            Point currentCenter = _filletGeometry.Center;
            Vector vec = new Vector(data.FilletCenter.X - currentCenter.X, data.FilletCenter.Y - currentCenter.Y);

            RectTopLeft = new Point(RectTopLeft.X + vec.X, RectTopLeft.Y + vec.Y);
            RectTopRight = new Point(RectTopRight.X + vec.X, RectTopRight.Y + vec.Y);
            RectBottomLeft = new Point(RectBottomLeft.X + vec.X, RectBottomLeft.Y + vec.Y);
            RectBottomRight = new Point(RectBottomRight.X + vec.X, RectBottomRight.Y + vec.Y);

            UpdateGeometry();
            IsGeometryRendered = true;
        }

        public FiberData GetMetaData()
        {
            double w = Math.Sqrt(Math.Pow(RectTopRight.X - RectTopLeft.X, 2.0) + Math.Pow(RectTopRight.Y - RectTopLeft.Y, 2.0));
            double h = Math.Sqrt(Math.Pow(RectBottomLeft.X - RectTopLeft.X, 2.0) + Math.Pow(RectBottomLeft.Y - RectTopLeft.Y, 2.0));

            return new FiberData()
            {
                FiberAngleInDeg = GetFiberAngleInDeg(),
                FilletCenter = _filletGeometry.Center,
                Width = w,
                Height = h
            };
        }

        private void UpdateGeometry()
        {
            _pathFigure.Segments.Clear();
            _pathFigure.StartPoint = RectTopLeft;
            _pathFigure.Segments.Add(new LineSegment(RectTopRight, true));
            _pathFigure.Segments.Add(new LineSegment(RectBottomRight, true));
            _pathFigure.Segments.Add(new LineSegment(RectBottomLeft, true));
            _pathFigure.Segments.Add(new LineSegment(RectTopLeft, true));

            Point topCenter = new Point((RectTopLeft.X + RectTopRight.X) / 2.0, (RectTopLeft.Y + RectTopRight.Y) / 2.0);
            TriangleApex = topCenter;

            double angleInDegrees = 90.0 - TriangleBottomEdgeAngleInDeg;
            Point pointLine1AnglePoint1 = GetIntersectionPoint_Line1AnglePoint(RectTopLeft, RectBottomLeft, TriangleApex, angleInDegrees);
            Point pointLine1AnglePoint2 = GetIntersectionPoint_Line1AnglePoint(RectTopRight, RectBottomRight, TriangleApex, -angleInDegrees);

            UpdateFilletCircle();

            _pathFigure.Segments.Add(new LineSegment(TriangleApex, true));
            _pathFigure.Segments.Add(new LineSegment(pointLine1AnglePoint2, true));
            _pathFigure.Segments.Add(new LineSegment(pointLine1AnglePoint1, true));
            _pathFigure.Segments.Add(new LineSegment(TriangleApex, true));

            Point point8 = new Point((RectBottomLeft.X + RectBottomRight.X) / 2.0, (RectBottomLeft.Y + RectBottomRight.Y) / 2.0);

            _pathFigure.Segments.Add(new LineSegment(topCenter, true));
            _pathFigure.Segments.Add(new LineSegment(point8, true));

            _topLeftHandle.GeometryCenter = RectTopLeft;
            _topRightHandle.GeometryCenter = RectTopRight;
            _bottomLeftHandle.GeometryCenter = RectBottomLeft;
            _bottomRightHandle.GeometryCenter = RectBottomRight;
            _triangleLeftBaseHandle.GeometryCenter = pointLine1AnglePoint1;
            _triangleRightBaseHandle.GeometryCenter = pointLine1AnglePoint2;
            _rotationHandle.GeometryCenter = point8;

            _fiberAngle = GetFiberAngleInDeg();
            UpdateVisual();
        }

        public override void OnMouseLeftButtonDown(Point mousePoint)
        {
            if (IsGeometryRendered)
            {
                FindSelectedHandle(mousePoint);
            }
            else
            {
                RectTopLeft = mousePoint;
                RectBottomRight = mousePoint;
                RectTopRight = mousePoint;
                RectBottomLeft = mousePoint;
                MouseDownPoint = mousePoint;
            }
            OldPointForTranslate = mousePoint;
        }

        public override void OnMouseMove(Point point, MouseButtonState buttonState)
        {
            if (buttonState != MouseButtonState.Pressed)
                return;

            if (!IsGeometryRendered)
            {
                RectBottomLeft = point;
                var (_, point1) = LineHelper.GetPerpendicularLineThroughPoint(RectTopLeft, RectBottomLeft, point);
                var (_, point2) = LineHelper.GetPerpendicularLineThroughPoint(RectTopLeft, RectBottomLeft, RectTopLeft);
                RectBottomRight = point1;
                RectTopRight = point2;

                _topLeftHandle.GeometryCenter = RectTopLeft;
                _topRightHandle.GeometryCenter = RectTopRight;
                _bottomLeftHandle.GeometryCenter = RectBottomLeft;
                _bottomRightHandle.GeometryCenter = RectBottomRight;
                _fiberAngle = GetFiberAngleInDeg();
            }
            else if (SelectedDragHandle != null)
            {
                switch (SelectedDragHandle.Id)
                {
                    case 1:
                        Vector v1_1 = new Vector(RectTopRight.X - RectTopLeft.X, RectTopRight.Y - RectTopLeft.Y);
                        Vector v1_2 = new Vector(RectBottomLeft.X - RectTopLeft.X, RectBottomLeft.Y - RectTopLeft.Y);
                        if (v1_1.Length > 0.0) v1_1.Normalize();
                        if (v1_2.Length > 0.0) v1_2.Normalize();
                        Vector vMouse1 = new Vector(point.X - RectBottomRight.X, point.Y - RectBottomRight.Y);
                        double p1_1 = -Vector.Multiply(vMouse1, v1_1);
                        double p1_2 = -Vector.Multiply(vMouse1, v1_2);
                        Vector r1_1 = Vector.Multiply(v1_1, p1_1);
                        Vector r1_2 = Vector.Multiply(v1_2, p1_2);
                        RectTopLeft = new Point(RectBottomRight.X - r1_1.X - r1_2.X, RectBottomRight.Y - r1_1.Y - r1_2.Y);
                        RectTopRight = new Point(RectBottomRight.X - r1_2.X, RectBottomRight.Y - r1_2.Y);
                        RectBottomLeft = new Point(RectBottomRight.X - r1_1.X, RectBottomRight.Y - r1_1.Y);
                        break;
                    case 2:
                        Vector v2_1 = new Vector(RectTopLeft.X - RectTopRight.X, RectTopLeft.Y - RectTopRight.Y);
                        Vector v2_2 = new Vector(RectBottomRight.X - RectTopRight.X, RectBottomRight.Y - RectTopRight.Y);
                        if (v2_1.Length > 0.0) v2_1.Normalize();
                        if (v2_2.Length > 0.0) v2_2.Normalize();
                        Vector vMouse2 = new Vector(point.X - RectBottomLeft.X, point.Y - RectBottomLeft.Y);
                        double p2_1 = -Vector.Multiply(vMouse2, v2_1);
                        double p2_2 = -Vector.Multiply(vMouse2, v2_2);
                        Vector r2_1 = Vector.Multiply(v2_1, p2_1);
                        Vector r2_2 = Vector.Multiply(v2_2, p2_2);
                        RectTopRight = new Point(RectBottomLeft.X - r2_1.X - r2_2.X, RectBottomLeft.Y - r2_1.Y - r2_2.Y);
                        RectTopLeft = new Point(RectBottomLeft.X - r2_2.X, RectBottomLeft.Y - r2_2.Y);
                        RectBottomRight = new Point(RectBottomLeft.X - r2_1.X, RectBottomLeft.Y - r2_1.Y);
                        break;
                    case 3:
                        Vector v3_1 = new Vector(RectTopRight.X - RectTopLeft.X, RectTopRight.Y - RectTopLeft.Y);
                        Vector v3_2 = new Vector(RectBottomLeft.X - RectTopLeft.X, RectBottomLeft.Y - RectTopLeft.Y);
                        if (v3_1.Length > 0.0) v3_1.Normalize();
                        if (v3_2.Length > 0.0) v3_2.Normalize();
                        Vector vMouse3 = new Vector(point.X - RectTopLeft.X, point.Y - RectTopLeft.Y);
                        double p3_1 = Vector.Multiply(vMouse3, v3_1);
                        double p3_2 = Vector.Multiply(vMouse3, v3_2);
                        Vector r3_1 = Vector.Multiply(v3_1, p3_1);
                        Vector r3_2 = Vector.Multiply(v3_2, p3_2);
                        RectBottomRight = new Point(RectTopLeft.X + r3_1.X + r3_2.X, RectTopLeft.Y + r3_1.Y + r3_2.Y);
                        RectTopRight = new Point(RectTopLeft.X + r3_1.X, RectTopLeft.Y + r3_1.Y);
                        RectBottomLeft = new Point(RectTopLeft.X + r3_2.X, RectTopLeft.Y + r3_2.Y);
                        break;
                    case 4:
                        Vector v4_1 = new Vector(RectTopLeft.X - RectTopRight.X, RectTopLeft.Y - RectTopRight.Y);
                        Vector v4_2 = new Vector(RectBottomRight.X - RectTopRight.X, RectBottomRight.Y - RectTopRight.Y);
                        if (v4_1.Length > 0.0) v4_1.Normalize();
                        if (v4_2.Length > 0.0) v4_2.Normalize();
                        Vector vMouse4 = new Vector(point.X - RectTopRight.X, point.Y - RectTopRight.Y);
                        double p4_1 = Vector.Multiply(vMouse4, v4_1);
                        double p4_2 = Vector.Multiply(vMouse4, v4_2);
                        Vector r4_1 = Vector.Multiply(v4_1, p4_1);
                        Vector r4_2 = Vector.Multiply(v4_2, p4_2);
                        RectBottomLeft = new Point(RectTopRight.X + r4_1.X + r4_2.X, RectTopRight.Y + r4_1.Y + r4_2.Y);
                        RectTopLeft = new Point(RectTopRight.X + r4_1.X, RectTopRight.Y + r4_1.Y);
                        RectBottomRight = new Point(RectTopRight.X + r4_2.X, RectTopRight.Y + r4_2.Y);
                        break;
                    case 7:
                    case 8:
                        if (OldPointForTranslate.HasValue)
                        {
                            TriangleBottomEdgeAngleInDeg = Math.Max(10.0, Math.Min(80.0, TriangleBottomEdgeAngleInDeg + (point.Y - OldPointForTranslate.Value.Y) * 0.1));
                            OldPointForTranslate = point;
                        }
                        break;
                    case 9:
                        if (OldPointForTranslate.HasValue)
                        {
                            Point topCenter = new Point((RectTopLeft.X + RectTopRight.X) / 2.0, (RectTopLeft.Y + RectTopRight.Y) / 2.0);
                            Point bottomCenter = new Point((RectBottomLeft.X + RectBottomRight.X) / 2.0, (RectBottomLeft.Y + RectBottomRight.Y) / 2.0);
                            Vector dir = new Vector(topCenter.X - bottomCenter.X, topCenter.Y - bottomCenter.Y);
                            if (dir.Length > 0.0)
                            {
                                dir.Normalize();
                                FilletRadius += -Vector.Multiply(new Vector(point.X - OldPointForTranslate.Value.X, point.Y - OldPointForTranslate.Value.Y), dir);
                            }
                            OldPointForTranslate = point;
                        }
                        break;
                    case 10:
                        if (OldPointForTranslate.HasValue)
                        {
                            Point center = _filletGeometry.Center;
                            Vector vOld = new Vector(OldPointForTranslate.Value.X - center.X, OldPointForTranslate.Value.Y - center.Y);
                            Vector vNew = new Vector(point.X - center.X, point.Y - center.Y);
                            if (vOld.Length > 0.0 && vNew.Length > 0.0)
                            {
                                double aOld = Math.Atan2(vOld.Y, vOld.X);
                                double aNew = Math.Atan2(vNew.Y, vNew.X);
                                RotateAboutAngle(aNew - aOld, center);
                            }
                            OldPointForTranslate = point;
                        }
                        break;
                }
            }
            else
            {
                HandleTranslate(point);
            }
        }

        private void RotateAboutAngle(double rotationAngle, Point rotationCenter)
        {
            RectTopLeft = RotatePointAroundCenter(RectTopLeft, rotationCenter, rotationAngle);
            RectTopRight = RotatePointAroundCenter(RectTopRight, rotationCenter, rotationAngle);
            RectBottomLeft = RotatePointAroundCenter(RectBottomLeft, rotationCenter, rotationAngle);
            RectBottomRight = RotatePointAroundCenter(RectBottomRight, rotationCenter, rotationAngle);
        }

        protected override void HandleTranslate(Point point)
        {
            if (!_enableTranslation || !OldPointForTranslate.HasValue)
                return;
            
            Point oldPoint = OldPointForTranslate.Value;
            double dx = point.X - oldPoint.X;
            double dy = point.Y - oldPoint.Y;
            
            RectBottomLeft = new Point(RectBottomLeft.X + dx, RectBottomLeft.Y + dy);
            RectTopLeft = new Point(RectTopLeft.X + dx, RectTopLeft.Y + dy);
            RectBottomRight = new Point(RectBottomRight.X + dx, RectBottomRight.Y + dy);
            RectTopRight = new Point(RectTopRight.X + dx, RectTopRight.Y + dy);
            
            OldPointForTranslate = point;
        }

        public static Point GetIntersectionPoint_Line1AnglePoint(
            Point line1Point1,
            Point line1Point2,
            Point pointOnLine2,
            double angleInDegrees)
        {
            double x = line1Point2.X - line1Point1.X;
            double y = line1Point2.Y - line1Point1.Y;
            double num1 = Math.Atan2(y, x) + angleInDegrees * Math.PI / 180.0;
            double num2 = Math.Cos(num1);
            double num3 = Math.Sin(num1);
            double num4 = x * -num3 - y * -num2;
            if (Math.Abs(num4) < 1E-10)
                return line1Point1;
            double num5 = pointOnLine2.X - line1Point1.X;
            double num6 = pointOnLine2.Y - line1Point1.Y;
            double num7 = Math.Max(0.0, Math.Min(1.0, (num5 * -num3 - num6 * -num2) / num4));
            return new Point(line1Point1.X + num7 * x, line1Point1.Y + num7 * y);
        }

        private void UpdateFilletCircle()
        {
            if (FilletRadius <= 0.0)
            {
                _filletGeometry.Center = new Point(0.0, 0.0);
                _filletGeometry.RadiusX = 0.0;
                _filletGeometry.RadiusY = 0.0;
            }
            else
            {
                double num = FilletRadius / Math.Sin((180.0 - 2.0 * TriangleBottomEdgeAngleInDeg) * Math.PI / 2.0 / 180.0);
                double angleInDegrees = 90.0 - TriangleBottomEdgeAngleInDeg;
                
                Point pointLine1AnglePoint1 = GetIntersectionPoint_Line1AnglePoint(RectTopLeft, RectBottomLeft, TriangleApex, angleInDegrees);
                Point pointLine1AnglePoint2 = GetIntersectionPoint_Line1AnglePoint(RectTopRight, RectBottomRight, TriangleApex, -angleInDegrees);
                
                Vector vector1 = new Vector(pointLine1AnglePoint1.X - TriangleApex.X, pointLine1AnglePoint1.Y - TriangleApex.Y);
                Vector vector2 = new Vector(pointLine1AnglePoint2.X - TriangleApex.X, pointLine1AnglePoint2.Y - TriangleApex.Y);
                if (vector1.Length > 0) vector1.Normalize();
                if (vector2.Length > 0) vector2.Normalize();
                
                Vector vector3 = new Vector((vector1.X + vector2.X) / 2.0, (vector1.Y + vector2.Y) / 2.0);
                if (vector3.Length > 0) vector3.Normalize();
                
                Point point = new Point(TriangleApex.X + vector3.X * num, TriangleApex.Y + vector3.Y * num);
                
                _filletGeometry.Center = point;
                _filletGeometry.RadiusX = FilletRadius;
                _filletGeometry.RadiusY = FilletRadius;
                _centerMarkHorizontal.StartPoint = new Point(point.X, point.Y - _crossSize / 2.0);
                _centerMarkHorizontal.EndPoint = new Point(point.X, point.Y + _crossSize / 2.0);
                _centerMarkVertical.StartPoint = new Point(point.X - _crossSize / 2.0, point.Y);
                _centerMarkVertical.EndPoint = new Point(point.X + _crossSize / 2.0, point.Y);
                _filletRadiusHandle.GeometryCenter = new Point(point.X + vector3.X * FilletRadius, point.Y + vector3.Y * FilletRadius);
            }
        }

        protected override void OnStrokeThicknessChanges(double strokeThickness)
        {
        }

        private Point RotatePointAroundCenter(Point point, Point center, double angleInRadians)
        {
            double dx = point.X - center.X;
            double dy = point.Y - center.Y;
            double cosA = Math.Cos(angleInRadians);
            double sinA = Math.Sin(angleInRadians);
            return new Point(dx * cosA - dy * sinA + center.X, dx * sinA + dy * cosA + center.Y);
        }

        private double AngleToRadian(double angle) => angle * Math.PI / 180.0;

        public override void UpdateVisual()
        {
            if (ShapeStyler == null)
                return;
            DrawingContext renderContext = RenderOpen();
            renderContext.DrawGeometry(Brushes.Transparent, null, RenderGeometry);
            renderContext.DrawGeometry(null, ShapeStyler.SketchPen, RenderGeometry);
            if (FilletRadius > 0.0)
            {
                renderContext.DrawGeometry(Brushes.Transparent, null, _filletGeometry);
                renderContext.DrawGeometry(null, ShapeStyler.SketchPen, _filletGeometry);
            }
            foreach (DragHandle handle in Handles)
                renderContext.DrawGeometry(ShapeStyler.FillColor, ShapeStyler.SketchPen, handle.HandleGeometry);
            DrawAnnotationText(renderContext);
            renderContext.Close();
        }

        private void DrawAnnotationText(DrawingContext renderContext)
        {
            DrawCircleText(renderContext);
            DrawRectText(renderContext);
        }

        private void DrawCircleText(DrawingContext renderContext)
        {
            double rScaled = 0.0;
            if (ShapeLayer.UnitsPerMillimeter != 0 && ShapeLayer.PixelPerUnit != 0.0)
                rScaled = FilletRadius * (double)ShapeLayer.UnitsPerMillimeter / ShapeLayer.PixelPerUnit;

            FormattedText fmtRadius = new FormattedText(
                $"radius: {rScaled:f0} {ShapeLayer.UnitName}",
                CultureInfo.GetCultureInfo("en-us"),
                FlowDirection.LeftToRight,
                new Typeface("Verdana"),
                (double)ShapeLayer.TagFontSize,
                Brushes.Red,
                96.0);

            FormattedText fmtAngle = new FormattedText(
                $"tip angle: {TriangleBottomEdgeAngleInDeg:f2}°",
                CultureInfo.GetCultureInfo("en-us"),
                FlowDirection.LeftToRight,
                new Typeface("Verdana"),
                (double)ShapeLayer.TagFontSize,
                Brushes.Red,
                96.0);

            double width = Math.Sqrt(Math.Pow(RectTopLeft.X - RectTopRight.X, 2.0) + Math.Pow(RectTopLeft.Y - RectTopRight.Y, 2.0));
            double wScaled = 0.0;
            if (ShapeLayer.UnitsPerMillimeter != 0 && ShapeLayer.PixelPerUnit != 0.0)
                wScaled = width * (double)ShapeLayer.UnitsPerMillimeter / ShapeLayer.PixelPerUnit;

            FormattedText fmtWidth = new FormattedText(
                $"width: {wScaled:f0} {ShapeLayer.UnitName}",
                CultureInfo.GetCultureInfo("en-us"),
                FlowDirection.LeftToRight,
                new Typeface("Verdana"),
                (double)ShapeLayer.TagFontSize,
                Brushes.Red,
                96.0);

            Vector vRot = new Vector(RectTopRight.X - RectTopLeft.X, RectTopRight.Y - RectTopLeft.Y);
            double angle = Math.Atan2(vRot.Y, vRot.X) * 180.0 / Math.PI;

            Vector norm = new Vector(-vRot.Y, vRot.X);
            if (norm.Length > 0.0) norm.Normalize();

            double padding = 2.0;    
            double baseYOffset = -110.0;

            double offset1 = baseYOffset + fmtWidth.Height + fmtAngle.Height + fmtRadius.Height + 2.0 * padding;
            Point o1 = new Point(RectTopLeft.X + norm.X * offset1, RectTopLeft.Y + norm.Y * offset1);

            double offset2 = baseYOffset + fmtAngle.Height + fmtRadius.Height + padding;
            Point o2 = new Point(RectTopLeft.X + norm.X * offset2, RectTopLeft.Y + norm.Y * offset2);

            double offset3 = baseYOffset + fmtRadius.Height;
            Point o3 = new Point(RectTopLeft.X + norm.X * offset3, RectTopLeft.Y + norm.Y * offset3);

            renderContext.PushTransform(new RotateTransform(angle, o1.X, o1.Y));
            renderContext.DrawText(fmtWidth, o1);
            renderContext.Pop();

            renderContext.PushTransform(new RotateTransform(angle, o2.X, o2.Y));
            renderContext.DrawText(fmtAngle, o2);
            renderContext.Pop();

            renderContext.PushTransform(new RotateTransform(angle, o3.X, o3.Y));
            renderContext.DrawText(fmtRadius, o3);
            renderContext.Pop();
        }

        private void DrawRectText(DrawingContext renderContext)
        {
            FormattedText fmtAngle = new FormattedText(
                $"Angle: {GetFiberAngleInDeg():f2}°",
                CultureInfo.GetCultureInfo("en-us"),
                FlowDirection.LeftToRight,
                new Typeface("Verdana"),
                (double)ShapeLayer.TagFontSize, 
                Brushes.Red,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double height = Math.Sqrt(Math.Pow(RectTopLeft.X - RectBottomLeft.X, 2.0) + Math.Pow(RectTopLeft.Y - RectBottomLeft.Y, 2.0));
            double hScaled = 0.0;
            if (ShapeLayer.UnitsPerMillimeter != 0 && ShapeLayer.PixelPerUnit != 0.0)
                hScaled = height * (double)ShapeLayer.UnitsPerMillimeter / ShapeLayer.PixelPerUnit;

            FormattedText fmtHeight = new FormattedText(
                $"Height: {hScaled:f0} {ShapeLayer.UnitName}",
                CultureInfo.GetCultureInfo("en-us"),
                FlowDirection.LeftToRight,
                new Typeface("Verdana"),
                (double)ShapeLayer.TagFontSize,
                Brushes.Red,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            Vector vRot = new Vector(RectBottomLeft.X - RectTopLeft.X, RectBottomLeft.Y - RectTopLeft.Y);
            double angle = Math.Atan2(vRot.Y, vRot.X) * 180.0 / Math.PI;

            Point midPoint = new Point((RectTopLeft.X + RectBottomLeft.X) / 2.0, (RectTopLeft.Y + RectBottomLeft.Y) / 2.0);

            Vector norm = new Vector(-vRot.Y, vRot.X);
            if (norm.Length > 0.0) norm.Normalize();

            int offset = 25;
            Point origin = new Point(midPoint.X + norm.X * offset, midPoint.Y + norm.Y * offset);

            renderContext.PushTransform(new RotateTransform(angle, origin.X, origin.Y));
            renderContext.DrawText(fmtAngle, origin);
            renderContext.Pop();

            renderContext.PushTransform(new RotateTransform(angle, origin.X, origin.Y));
            renderContext.DrawText(fmtHeight, new Point(origin.X, origin.Y - fmtHeight.Height - 5.0));
            renderContext.Pop();
        }

        private double GetFiberAngleInDeg()
        {
            Vector vector = new Vector(RectTopLeft.X - RectBottomLeft.X, RectTopLeft.Y - RectBottomLeft.Y);
            return -(Math.Atan2(vector.Y, vector.X) * 180.0 / Math.PI);
        }
    }
}