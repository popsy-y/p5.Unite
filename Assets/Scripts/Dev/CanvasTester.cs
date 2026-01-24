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
            _cnv.CreateCanvas(DrawingMode.ScreenSpace);
        }

        private void Update()
        {
            var hw = _cnv.Width / 2f;
            var hh = _cnv.Height / 2f;
            
            for (int i = -5; i < 11; i++)
            {
                var phase = Mathf.Sin(Time.time * 10f + (i * (Mathf.PI / 10f)));
                var width = 100 + phase * 200;
                
                _cnv.Rect(
                    hw,
                    hh + i * 35f,
                    i % 2 == 0 ? 0f : 1f,
                    width,
                    3f
                );
            }
        }
    }
}