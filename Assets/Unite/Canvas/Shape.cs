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
        // Quad,
        Rectangle,
        Square,
        // Triangle // beginShape, vertex, endShape対応まで保留
    }

    public partial class UCanvas
    {
        private Vector3 FinalPos(Vector3 localPos) => TransformPoint(localPos);
        private (Vector3 rot, Vector2 scl) RotScl() => GetRotationAndScale();

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
                var rPos = FinalPos(pos);
                var rSize = size;

                return (rPos, rSize);
            }
        }

        #region Circle

        /// <summary>
        /// Draws a circle at the specified 2D position with the given radius.
        /// </summary>
        /// <param name="x">X-coordinate.</param>
        /// <param name="y">Y-coordinate.</param>
        /// <param name="radius">Radius of the circle.</param>
        public void Circle(float x, float y, float radius)
        {
            CheckDrawingContext();

            // float fRot;
            // TODO: also support 3d size
            Vector2 fSize;
            Vector3 fPos, fRot;

            if (_drawingContext.DrawingMode is DrawingMode.WorldSpace or DrawingMode.ObjectSpace)
            {
                var (r, s) = RotScl();
                fPos = FinalPos(new Vector3(x, y, 0f));
                fRot = r;
                fSize = Vector2.one * radius * s;
            }
            else
            {
                var (r, s) = RotScl();
                var (nPos, nSize) = EnsureNormalizedValue(new Vector3(x, y, 0f), Vector2.one * radius);
                fPos = nPos;

                // NOTE: 平面時はrx, ryを潰すこともできるが、
                // そこからハックが生まれそうだし、ライブラリの性格を考えると暗黙的な挙動が増えるべきではないと思ったので意図的にそのまま残した。
                // その代わりに、平面用にfloatのみを受け付けるRotateメソッドが用意してある。
                fRot = r;
                fSize = nSize * s;
            }

            _commandQueue.Enqueue(new DrawCommand
            {
                Position = fPos,
                Rotation = fRot,
                Size = fSize,
                ShapeType = Shape.Circle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }

        /// <summary>
        /// Draws a circle at the specified 2D position with the given radius.
        /// </summary>
        /// <param name="position">2D position.</param>
        /// <param name="radius">Radius of the circle.</param>
        public void Circle(Vector2 position, float radius)
        {
            Circle(position.x, position.y, radius);
        }

        /// <summary>
        /// Draws a circle at the specified 3D position with the given radius.
        /// </summary>
        /// <param name="x">X-coordinate.</param>
        /// <param name="y">Y-coordinate.</param>
        /// <param name="z">Z-coordinate (depth).</param>
        /// <param name="radius">Radius of the circle.</param>
        public void Circle(float x, float y, float z, float radius)
        {
            CheckDrawingContext();

            Vector3 fRot;
            Vector2 fSize;
            Vector3 fPos;

            if (_drawingContext.DrawingMode is DrawingMode.WorldSpace or DrawingMode.ObjectSpace)
            {
                var (r, s) = RotScl();
                fPos = FinalPos(new Vector3(x, y, z));
                fRot = r;
                fSize = Vector2.one * radius * s;
            }
            else
            {
                var (r, s) = RotScl();
                var (nPos, nSize) = EnsureNormalizedValue(new Vector3(x, y, z), Vector2.one * radius);
                fPos = nPos;
                fRot = r;
                fSize = nSize * s;
            }

            _commandQueue.Enqueue(new DrawCommand
            {
                Position = fPos,
                Rotation = fRot,
                Size = fSize,
                ShapeType = Shape.Circle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }

        /// <summary>
        /// Draws a circle at the specified 3D position with the given radius.
        /// </summary>
        /// <param name="position">3D position.</param>
        /// <param name="radius">Radius of the circle.</param>
        public void Circle(Vector3 position, float radius)
        {
            Circle(position.x, position.y, position.z, radius);
        }
        #endregion

        #region Rectangle

        /// <summary>
        /// Draws a rectangle at the specified 2D position with the given dimensions.
        /// </summary>
        /// <param name="x">X-coordinate.</param>
        /// <param name="y">Y-coordinate.</param>
        /// <param name="width">Width of the rectangle.</param>
        /// <param name="height">Height of the rectangle.</param>
        public void Rect(float x, float y, float width, float height)
        {
            CheckDrawingContext();

            Vector3 fRot;
            Vector2 fSize;
            Vector3 fPos;

            if (_drawingContext.DrawingMode is DrawingMode.WorldSpace or DrawingMode.ObjectSpace)
            {
                var (r, s) = RotScl();
                fPos = FinalPos(new Vector3(x, y, 0f));
                fRot = r;
                fSize = new Vector2(width, height) * s;
            }
            else
            {
                var (r, s) = RotScl();
                var (nPos, nSize) = EnsureNormalizedValue(new Vector3(x, y, 0f), new Vector2(width, height));
                fPos = nPos;
                fRot = r;
                fSize = nSize * s;
            }

            _commandQueue.Enqueue(new DrawCommand()
            {
                Position = fPos,
                Rotation = fRot,
                Size = fSize,
                ShapeType = Shape.Rectangle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }

        /// <summary>
        /// Draws a rectangle at the specified 2D position with the given dimensions.
        /// </summary>
        /// <param name="position">2D position.</param>
        /// <param name="size">Width and height of the rectangle.</param>
        public void Rect(Vector2 position, Vector2 size)
        {
            Rect(position.x, position.y, size.x, size.y);
        }

        /// <summary>
        /// Draws a rectangle at the specified 3D position with the given dimensions.
        /// </summary>
        /// <param name="x">X-coordinate.</param>
        /// <param name="y">Y-coordinate.</param>
        /// <param name="z">Z-coordinate (depth).</param>
        /// <param name="width">Width of the rectangle.</param>
        /// <param name="height">Height of the rectangle.</param>
        public void Rect(float x, float y, float z, float width, float height)
        {
            CheckDrawingContext();

            Vector3 fRot;
            Vector2 fSize;
            Vector3 fPos;

            if (_drawingContext.DrawingMode is DrawingMode.WorldSpace or DrawingMode.ObjectSpace)
            {
                var (r, s) = RotScl();
                fPos = FinalPos(new Vector3(x, y, z));
                fRot = r;
                fSize = new Vector2(width, height) * s;
            }
            else
            {
                var (r, s) = RotScl();
                var (nPos, nSize) = EnsureNormalizedValue(new Vector3(x, y, z), new Vector2(width, height));
                fPos = nPos;
                fRot = r;
                fSize = nSize * s;
            }

            _commandQueue.Enqueue(new DrawCommand()
            {
                Position = fPos,
                Rotation = fRot,
                Size = fSize,
                ShapeType = Shape.Rectangle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }

        /// <summary>
        /// Draws a rectangle at the specified 3D position with the given dimensions.
        /// </summary>
        /// <param name="position">3D position.</param>
        /// <param name="size">Width and height of the rectangle.</param>
        public void Rect(Vector3 position, Vector2 size)
        {
            Rect(position.x, position.y, position.z, size.x, size.y);
        }
        #endregion

        #region Square

        /// <summary>
        /// Draws a square at the specified 2D position with the given size.
        /// </summary>
        /// <param name="x">X-coordinate.</param>
        /// <param name="y">Y-coordinate.</param>
        /// <param name="size">Size of the square (width and height).</param>
        public void Square(float x, float y, float size)
        {
            CheckDrawingContext();

            Vector3 fRot;
            Vector2 fSize;
            Vector3 fPos;

            if (_drawingContext.DrawingMode is DrawingMode.WorldSpace or DrawingMode.ObjectSpace)
            {
                // World Space / Object Space
                // expects unity unit value in world space

                var (r, s) = RotScl();

                fPos = FinalPos(new Vector3(x, y, 0f));
                fRot = r;
                fSize = Vector2.one * size * s;
            }
            else
            {
                // Clip Space
                // expects normalized value in clip space

                var (r, s) = RotScl();
                var (nPos, nSize) = EnsureNormalizedValue(new Vector3(x, y, 0f), Vector2.one * size);

                // apply canvas transformation scale
                fPos = nPos;
                fRot = r;
                fSize = nSize * s;
            }

            _commandQueue.Enqueue(new DrawCommand()
            {
                Position = fPos,
                Rotation = fRot,
                Size = fSize,
                ShapeType = Shape.Rectangle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }

        /// <summary>
        /// Draws a square at the specified 2D position with the given size.
        /// </summary>
        /// <param name="position">2D position.</param>
        /// <param name="size">Size of the square (width and height).</param>
        public void Square(Vector2 position, float size)
        {
            Square(position.x, position.y, size);
        }

        /// <summary>
        /// Draws a square at the specified 3D position with the given size.
        /// </summary>
        /// <param name="x">X-coordinate.</param>
        /// <param name="y">Y-coordinate.</param>
        /// <param name="z">Z-coordinate (depth).</param>
        /// <param name="size">Size of the square (width and height).</param>
        public void Square(float x, float y, float z, float size)
        {
            CheckDrawingContext();

            Vector3 fRot;
            Vector2 fSize;
            Vector3 fPos;

            if (_drawingContext.DrawingMode is DrawingMode.WorldSpace or DrawingMode.ObjectSpace)
            {
                var (r, s) = RotScl();
                fPos = FinalPos(new Vector3(x, y, z));
                fRot = r;
                fSize = Vector2.one * size * s;
            }
            else
            {
                var (r, s) = RotScl();
                var (nPos, nSize) = EnsureNormalizedValue(new Vector3(x, y, z), Vector2.one * size);
                fPos = nPos;
                fRot = r;
                fSize = nSize * s;
            }

            _commandQueue.Enqueue(new DrawCommand()
            {
                Position = fPos,
                Rotation = fRot,
                Size = fSize,
                ShapeType = Shape.Rectangle,
                FillColor = _fillColor,
                StrokeColor = _strokeColor
            });
        }

        /// <summary>
        /// Draws a square at the specified 3D position with the given size.
        /// </summary>
        /// <param name="position">3D position.</param>
        /// <param name="size">Size of the square (width and height).</param>
        public void Square(Vector3 position, float size)
        {
            Square(position.x, position.y, position.z, size);
        }
        #endregion
    }
}

