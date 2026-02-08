using System;
using Unite.Canvas;
using Unite.Drawing;
using UnityEngine;

namespace Dev
{
    public class SampleDrawing : MonoBehaviour
    {
        private UCanvas _cnv;

        private void OnEnable()
        {
            _cnv = GetComponent<UCanvas>();
            if (!_cnv)
                _cnv = gameObject.AddComponent<UCanvas>();

            _cnv.CreateCanvas(DrawingMode.ScreenSpace);
        }


    }
}