using UnityEngine;

namespace Unite.Canvas
{
    public enum Shape
    {
        // Arc,
        Circle,
        // Ellipse,
        // Line,
        // Point,
        // Quad,
        Rectangle,
        Square,
        Triangle
    }

    public partial class Canvas
    {
        private Vector3 FinalPos(Vector3 localPos) => TransformPoint(localPos);
        private (float rot, Vector2 scl) RotScl() => GetRotationAndScale();
        
        public void Circle(float x, float y, float radius)
        {
            CheckDrawingContext();

            var pos = FinalPos(new  Vector3(x, y, 0));
            var (r, s) = RotScl();
            
            _commandQueue.Enqueue(new DrawCommand
            {
                Position = pos,
                Rotation = r,
                Size = new Vector2(radius * s.x, radius * s.y),
                    
                ShapeType = Shape.Circle,
                
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }

        public void Rect(float x, float y, float width, float height)
        {
            CheckDrawingContext();
            
            var pos = FinalPos(new  Vector3(x, y, 0));
            var (r, s) = RotScl();
            
            _commandQueue.Enqueue(new DrawCommand()
            {
                Position = pos,
                Rotation = r,
                Size = new Vector2(width * s.x, height * s.y),
                
                ShapeType = Shape.Rectangle,
                
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }

        public void Square(float x, float y, float size)
        {
            CheckDrawingContext();
            
            var pos = FinalPos(new  Vector3(x, y, 0));
            var (r, s) = RotScl();
            
            _commandQueue.Enqueue(new DrawCommand()
            {
                Position = pos,
                Rotation = r,
                Size = new Vector2(size * s.x, size * s.y),
                
                ShapeType = Shape.Rectangle,
                
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }
    }
}