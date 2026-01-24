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
            for (int i = 0; i < 10; i++)
            {
                _cnv.Square(
                    30 + 40*i,
                    30 + Mathf.Sin(Time.time*10f + (i * (Mathf.PI / 10f))) * 10f,
                    10
                );
            }
        }
    }
}