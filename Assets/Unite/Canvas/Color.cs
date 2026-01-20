using UnityEngine;

namespace Unite.Canvas
{
    public partial class Canvas
    {
        public void Fill(Color color) => _fillColor =  color;

        public void Stroke(Color color) => _strokeColor = color;
    }
}