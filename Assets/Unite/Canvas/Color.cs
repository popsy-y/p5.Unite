using UnityEngine;

namespace Unite.Canvas
{
    public partial class UCanvas
    {
        public void Fill(Color color)
        {
            _styleState.FillColor = color;
            _styleState.FillEnabled = true;
        }

        public void NoFill() => _styleState.FillEnabled = false;

        public void Stroke(Color color)
        {
            _styleState.StrokeColor = color;
            _styleState.StrokeEnabled = true;
        }

        public void NoStroke() => _styleState.StrokeEnabled = false;

        public void StrokeWeight(float weight) => _styleState.StrokeWeight = weight;
    }
}
