using System.Windows.Media;

namespace Lan.Shapes.Styler
{
    public class ShapeStylerFactory : IShapeStylerFactory
    {
        private IShapeStyler _defaultStyler;
        private IShapeStyler _dottedLineStyler;
        private IShapeStyler _selectedStyler;


        public IShapeStyler ShapeUnselectedVisualState()
        {
            if (_defaultStyler == null)
            {
                _defaultStyler = new ShapeStyler();
                _defaultStyler.SetFillColor(Brushes.Transparent);
                _defaultStyler.SetStrokeColor(Brushes.Blue);
                _defaultStyler.SetStrokeThickness(1);
                _defaultStyler.Name = "未选中";
            }

            return _defaultStyler;
        }

        public void SetStylerProperties(IShapeStyler shapeStyler, Brush fillColor, Brush strokeColor, double thickness)
        {
            shapeStyler.SetFillColor(fillColor);
            shapeStyler.SetStrokeColor(strokeColor);
            shapeStyler.SetStrokeThickness(thickness);
        }

        public IShapeStyler ShapeSelectedVisualState()
        {
            if (_selectedStyler == null)
            {
                _selectedStyler = new ShapeStyler();
                _selectedStyler.SetFillColor(Brushes.LightYellow);
                _selectedStyler.SetStrokeColor(Brushes.Red);
                _selectedStyler.SetStrokeThickness(1);
                _selectedStyler.Name = "选中";
            }

            return _selectedStyler;
        }

        public IShapeStyler DottedLineStyler()
        {
            if (_dottedLineStyler == null)
            {
                _dottedLineStyler = new ShapeStyler();
                _dottedLineStyler.SetFillColor(Brushes.LightYellow);
                _dottedLineStyler.SetStrokeColor(Brushes.Green);
                _dottedLineStyler.SetStrokeThickness(1);
                _dottedLineStyler.SetPenDashStyle(DashStyles.Dash);
                _dottedLineStyler.Name = "dotted line";
            }

            return _dottedLineStyler;
        }


        public IShapeStyler CustomShapeStyler(Brush fillColor, Brush strokeColor, double strokeThickness)
        {
            var customStyler = new ShapeStyler();
            customStyler.SetFillColor(fillColor);
            customStyler.SetStrokeColor(strokeColor);
            customStyler.SetStrokeThickness(strokeThickness);

            return customStyler;
        }   
        
        public IShapeStyler CustomShapeStyler(Brush fillColor, Brush strokeColor, double strokeThickness,double dragHandleSize)
        {
            var customStyler = new ShapeStyler();
            customStyler.SetFillColor(fillColor);
            customStyler.SetStrokeColor(strokeColor);
            customStyler.SetStrokeThickness(strokeThickness);
            customStyler.DragHandleSize = dragHandleSize;
            return customStyler;
        }
    }
}