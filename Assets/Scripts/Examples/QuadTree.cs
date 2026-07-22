using UnityEngine;

using Unite.Canvas;
using Unite.Drawing;

namespace Dev
{
    public class QuadTree : MonoBehaviour
    {
        [SerializeField] private UCanvas c;
        [SerializeField] private float far = .1f;

        private const int maxDepth = 5;

        private Unity.Mathematics.Random _rng;
        private float _interval = 0.4839f;

        private int nBaseGridRes = 10;

        private static Color[] colors = new Color[]
        {
            new Color(189/255f, 68/255f, 68/255f),
            new Color(241/255f, 222/255f, 196/255f),
            new Color(115/255f, 151/255f, 106/255f),
            new Color(103/255f, 126/255f, 97/255f),
        };

        void Start()
        {
            c.CreateCanvas(DrawingMode.ScreenSpace, DrawingExecutionMode.Indirect);
        }

        void Update()
        {
            var loop = Mathf.FloorToInt(Time.time / _interval);
            var frac = Time.time % _interval / _interval;

            _rng = new Unity.Mathematics.Random((uint)(loop + 1));

            var currentMaxDepth = Mathf.FloorToInt(maxDepth * frac);

            var stepX = c.Width / nBaseGridRes;
            var stepY = c.Height / nBaseGridRes;

            for (int x = 0; x < nBaseGridRes; x++)
            for (int y = 0; y < nBaseGridRes; y++)
            {
                Cell(stepX * x, stepY * y, c.Width/nBaseGridRes, c.Height/nBaseGridRes, 0, currentMaxDepth);
            }
        }

        private void Cell(int x, int y, int width, int height, int depth, int currentMaxDepth)
        {
            if (depth < currentMaxDepth)
            {
                var rnd = _rng.NextFloat();

                if (rnd < .1f)
                {
                    Cell(x, y,          width, height/2, depth+1, currentMaxDepth);
                    Cell(x, y+height/2, width, height/2, depth+1, currentMaxDepth);
                }
                else if (rnd < .2f)
                {
                    Cell(x,         y, width/2, height, depth+1, currentMaxDepth);
                    Cell(x+width/2, y, width/2, height, depth+1, currentMaxDepth);
                }
                else if (rnd < .6f)
                {
                    Cell(x,         y, width/2, height/2, depth+1, currentMaxDepth);
                    Cell(x+width/2, y, width/2, height/2, depth+1, currentMaxDepth);
                    Cell(x,         y+height/2, width/2, height/2, depth+1, currentMaxDepth);
                    Cell(x+width/2, y+height/2, width/2, height/2, depth+1, currentMaxDepth);
                }
                else
                {
                    // c.Rect(x, y, width, height);
                }
            }
            else
            {
                c.Fill(colors[Mathf.FloorToInt(_rng.NextFloat() * colors.Length)]);
                c.Rect(new Vector3(x, y, depth/(float)maxDepth*far), new Vector2(width, height));
            }
        }
    }
}