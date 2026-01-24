using Unite.Drawing;
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
        // Triangle // beginShape, vertex, endShape対応まで保留
    }

    public partial class UCanvas
    {
        private Vector3 FinalPos(Vector3 localPos) => TransformPoint(localPos);
        private (float rot, Vector2 scl) RotScl() => GetRotationAndScale();

        private Vector2 NanToZero(Vector2 v)
        {
            return new Vector2(
                float.IsNaN(v.x) ? 0f : v.x,
                float.IsNaN(v.y) ? 0f : v.y
            );
        }
        private Vector3 NanToZero(Vector3 v)
        {
            return new Vector3(
                float.IsNaN(v.x) ? 0f : v.x,
                float.IsNaN(v.y) ? 0f : v.y,
                float.IsNaN(v.z) ? 0f : v.z
            );
        }

        /// <summary>
        /// Store values in normalized value. 
        /// We often have to deal with multiple camera with different resolutions later.
        /// Pass values before apply canvas transformation.
        /// </summary>
        /// <param name="pos">raw position value.</param>
        /// <param name="size">raw size value.</param>
        /// <returns></returns>
        private (Vector3 pos, Vector2 size) EnsureNormalizedValue(Vector3 pos, Vector2 size)
        {
            if (_drawingContext.DrawingMode is DrawingMode.ScreenSpace or DrawingMode.ScreenSpaceOverlay)
            {
                var fPos = FinalPos(new Vector3(pos.x, pos.y, 1));
                var rPos = new Vector3(
                    fPos.x / Width,
                    fPos.y / Height,
                    pos.z
                );
                
                var rSize = new Vector2(size.x / Width, size.y / Height);
                
                return (rPos, rSize);
            }
            else
            {
                var rPos = FinalPos(new Vector3(pos.x, pos.y, pos.z));
                var rSize = size;

                return (rPos, rSize);
            }
        }
        
        #region Circle

        public void Circle(float x, float y, float radius)
        {
            CheckDrawingContext();
            
            var (r, s) = RotScl();
            var (pos, size) = EnsureNormalizedValue(new Vector3(x, y, 1f), Vector2.one * radius);
            
            _commandQueue.Enqueue(new DrawCommand
            {
                Position = pos,
                Rotation = r,
                Size = size * s,
                ShapeType = Shape.Circle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }

        public void Circle(Vector2 position, float radius)
        {
            CheckDrawingContext();

            var (r, s) = RotScl();
            var (pos, size) = EnsureNormalizedValue(new Vector3(position.x, position.y, 1f), Vector2.one * radius);

            _commandQueue.Enqueue(new DrawCommand
            {
                Position = pos,
                Rotation = r,
                Size = size * s,
                ShapeType = Shape.Circle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }

        public void Circle(float x, float y, float z, float radius)
        {
            CheckDrawingContext();

            var (r, s) = RotScl();
            var (pos, size) = EnsureNormalizedValue(new Vector3(x, y, z), Vector2.one * radius);

            _commandQueue.Enqueue(new DrawCommand
            {
                Position = pos,
                Rotation = r,
                Size = size * s,
                ShapeType = Shape.Circle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }

        public void Circle(Vector3 position, float radius)
        {
            CheckDrawingContext();

            var (r, s) = RotScl();
            var (pos, size) = EnsureNormalizedValue(position, Vector2.one * radius);

            _commandQueue.Enqueue(new DrawCommand
            {
                Position = pos,
                Rotation = r,
                Size = size * s,
                ShapeType = Shape.Circle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }
        #endregion

        #region Rectangle

        public void Rect(float x, float y, float width, float height)
        {
            CheckDrawingContext();
            
            var (r, s) = RotScl();
            var (pos, size) = EnsureNormalizedValue(new Vector3(x, y, 1f), new Vector2(width, height));
            
            _commandQueue.Enqueue(new DrawCommand()
            {
                Position = pos,
                Rotation = r,
                Size = size * s,
                ShapeType = Shape.Rectangle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }

        public void Rect(Vector2 position, Vector2 size)
        {
            CheckDrawingContext();

            var (r, s) = RotScl();
            var (pos, normSize) = EnsureNormalizedValue(new Vector3(position.x, position.y, 1f), size);

            _commandQueue.Enqueue(new DrawCommand()
            {
                Position = pos,
                Rotation = r,
                Size = normSize * s,
                ShapeType = Shape.Rectangle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }

        public void Rect(float x, float y, float z, float width, float height)
        {
            CheckDrawingContext();

            var (r, s) = RotScl();
            var (pos, size) = EnsureNormalizedValue(new Vector3(x, y, z), new Vector2(width, height));

            _commandQueue.Enqueue(new DrawCommand()
            {
                Position = pos,
                Rotation = r,
                Size = size * s,
                ShapeType = Shape.Rectangle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }

        public void Rect(Vector3 position, Vector2 size)
        {
            CheckDrawingContext();

            var (r, s) = RotScl();
            var (pos, normSize) = EnsureNormalizedValue(position, size);

            _commandQueue.Enqueue(new DrawCommand()
            {
                Position = pos,
                Rotation = r,
                Size = normSize * s,
                ShapeType = Shape.Rectangle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }
        #endregion

        #region Square

        public void Square(float x, float y, float size)
        {
            CheckDrawingContext();
            
            var (r, s) = RotScl();
            var (pos, normSize) = EnsureNormalizedValue(new Vector3(x, y, 1f), Vector2.one * size);
            
            _commandQueue.Enqueue(new DrawCommand()
            {
                Position = pos,
                Rotation = r,
                Size = normSize * s,
                ShapeType = Shape.Rectangle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }

        public void Square(Vector2 position, float size)
        {
            CheckDrawingContext();

            var (r, s) = RotScl();
            var (pos, normSize) = EnsureNormalizedValue(new Vector3(position.x, position.y, 1f), Vector2.one * size);

            _commandQueue.Enqueue(new DrawCommand()
            {
                Position = pos,
                Rotation = r,
                Size = normSize * s,
                ShapeType = Shape.Rectangle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }

        public void Square(float x, float y, float z, float size)
        {
            CheckDrawingContext();

            var (r, s) = RotScl();
            var (pos, normSize) = EnsureNormalizedValue(new Vector3(x, y, z), Vector2.one * size);

            _commandQueue.Enqueue(new DrawCommand()
            {
                Position = pos,
                Rotation = r,
                Size = normSize * s,
                ShapeType = Shape.Rectangle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }

        public void Square(Vector3 position, float size)
        {
            CheckDrawingContext();

            var (r, s) = RotScl();
            var (pos, normSize) = EnsureNormalizedValue(position, Vector2.one * size);

            _commandQueue.Enqueue(new DrawCommand()
            {
                Position = pos,
                Rotation = r,
                Size = normSize * s,
                ShapeType = Shape.Rectangle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }
        #endregion
    }
}

