using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SoftBackdrop : MaskableGraphic
    {
        public bool TableSurface { get; set; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            const int count = 20;
            for (int y = 0; y <= count; y++)
            for (int x = 0; x <= count; x++)
            {
                float u = x / (float)count, v = y / (float)count;
                float glow = Mathf.Clamp01(1 - Vector2.Distance(new Vector2(u, v), new Vector2(.48f, .66f)) * 1.6f);
                vh.AddVert(new Vector2(r.xMin + u * r.width, r.yMin + v * r.height),
                    TableSurface ? Color.Lerp(DominoVisualTheme.Table, DominoVisualTheme.TableLight, glow * .85f)
                    : Color.Lerp(DominoVisualTheme.Background, DominoVisualTheme.BackgroundLight, glow), Vector2.zero);
                if (x < count && y < count)
                {
                    int i = y * (count + 1) + x;
                    vh.AddTriangle(i, i + count + 1, i + 1);
                    vh.AddTriangle(i + 1, i + count + 1, i + count + 2);
                }
            }
        }
    }
}
