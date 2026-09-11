using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SoftBackdrop : MaskableGraphic
    {
        public bool TableSurface { get; set; }
        static Texture2D weave;
        public override Texture mainTexture
        {
            get
            {
                if (!TableSurface) return Texture2D.whiteTexture;
                if (weave) return weave;
                weave = new Texture2D(128,128,TextureFormat.RGB24,false) { name="Subtle felt weave", wrapMode=TextureWrapMode.Repeat, filterMode=FilterMode.Bilinear, hideFlags=HideFlags.HideAndDontSave };
                var pixels = new Color[128*128];
                for (int y=0;y<128;y++) for (int x=0;x<128;x++)
                {
                    float grain = ((x*73+y*151+x*y*17)%97)/96f;
                    float value = .975f + grain*.018f + ((x+y)%2)*.007f;
                    pixels[y*128+x] = new Color(value,value,value,1);
                }
                weave.SetPixels(pixels); weave.Apply(false,true);
                return weave;
            }
        }
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
                    : Color.Lerp(DominoVisualTheme.Background, DominoVisualTheme.BackgroundLight, glow), TableSurface ? new Vector2(u*r.width/128,v*r.height/128) : Vector2.zero);
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
