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

        // because i needed to make commit for testing reason
        private void Update()
        {
            var hw = _cnv.Width / 2f;
            var hh = _cnv.Height / 2f;
            
            for (int i = -5; i < 11; i++)
            {
                _cnv.Push();
                _cnv.Translate(.3f, .5f);
                // _cnv.Scale((i + 5) / 11f, 2f, 1f);
                _cnv.Rotate(new Vector3(0f, 0f, (Mathf.PI / 10) * i));
                // _cnv.Rotate((Mathf.PI / 11) * (i + 5));
                
                var phase = Mathf.Sin(Time.time * 10f + (i * (Mathf.PI / 10f)));
                var width = 100 + phase * 200;
                
                // Clip space
                // _cnv.Rect(0f, 0f, i % 2 == 0 ? 0f : 1f, width / _cnv.Width, 3f / _cnv.Height);
                // _cnv.Rect(
                //     hw / _cnv.Width,
                //     (hh + i * 35f) / _cnv.Height,
                //     i % 2 == 0 ? 0f : 1f,
                //     width / _cnv.Width,
                //     3f / _cnv.Height
                // );
                
                // Screen Space
                _cnv.Rect(0f, 0f, i % 2 == 0 ? 0f : 1f, width * 2f, 3f);
                // _cnv.Rect(
                //     hw,
                //     hh + i * 35f,
                //     i % 2 == 0 ? 0f : 1f,
                //     width,
                //     3f
                // );
                
                // World Space
                // _cnv.Rect(
                //     0,
                //     i / 5f,
                //     i % 2 == 0 ? -1f : 1f,
                //     width / 100f,
                //     .1f
                // );
                
                _cnv.Pop();
            }
            
            // world space depth
            // TODO: フレームドロップする場面がある、多分極めて小さい値の計算がされている？
            // そして今度は後ろにしか表示されない。
            // Reverse-Zになっているが、p5.jsのWEBGLモードではnear-far 0-1のようであるため、変換をメソッド内で行う
            
            // depthが正しくない？一度動くとその後キューブが隠れたままになる
            
            var depth = _cnv.WorldToClipDepth(Mathf.Repeat(Time.time, 5f));
            if (Time.frameCount % 10 == 0) Debug.Log(depth);
            _cnv.Rect(
                hw, hh,
                depth,
                300f, 300f
            );
            
            // _cnv.Square(0, 0, 2);
        }
    }
}