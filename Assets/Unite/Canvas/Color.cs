using UnityEngine;

namespace Unite.Canvas
{
    public partial class UCanvas
    {
        public void Fill(Color color)
        {
            _fillColor = color;
            _fillEnabled = true;
        }

        public void NoFill() => _fillEnabled = false;

        public void Stroke(Color color)
        {
            _strokeColor = color;
            _strokeEnabled = true;
        }

        public void NoStroke() => _strokeEnabled = false;

        public void StrokeWeight(float weight) => _strokeWeight = weight;
    }
}
