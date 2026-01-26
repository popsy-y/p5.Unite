using Unite.Canvas;
using Unite.Drawing;
using UnityEngine;

namespace Dev
{
    public class CanvasTester : MonoBehaviour
    {
        private UCanvas _cnv;
        
        private void Start()
        {
            _cnv = GetComponent<UCanvas>();
            _cnv.CreateCanvas(DrawingMode.WorldSpace);
        }

        private void Update()
        {
            var hw = _cnv.Width / 2f;
            var hh = _cnv.Height / 2f;
            
            for (int i = -5; i < 11; i++)
            {
                var phase = Mathf.Sin(Time.time * 10f + (i * (Mathf.PI / 10f)));
                var width = 100 + phase * 200;
                
                // Clip space
                // _cnv.Rect(
                //     hw / _cnv.Width,
                //     (hh + i * 35f) / _cnv.Height,
                //     i % 2 == 0 ? 0f : 1f,
                //     width / _cnv.Width,
                //     3f / _cnv.Height
                // );
                
                // Screen Space
                // _cnv.Rect(
                //     hw,
                //     hh + i * 35f,
                //     i % 2 == 0 ? 0f : 1f,
                //     width,
                //     3f
                // );
                
                // World Space
                _cnv.Rect(
                    0,
                    i / 5f,
                    i % 2 == 0 ? -1f : 1f,
                    width / 100f,
                    .1f
                );
            }
            
            // _cnv.Square(0, 0, 2);
        }
    }
}