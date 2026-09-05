using System;
using Domino.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Domino.UI
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class DominoTileView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField, Range(0, 9)] int previewA = 6;
        [SerializeField, Range(0, 9)] int previewB = 4;
        [SerializeField] bool faceUp = true;
        [SerializeField] bool vertical;
        DominoFace graphic;
        Text backBrand;
        Image backLogo;
        void OnEnable() { TileStyles.Changed += Refresh; if (graphic) Refresh(); }
        void OnDisable() => TileStyles.Changed -= Refresh;
        public DominoTile Tile { get; private set; }
        public RectTransform Rect => (RectTransform)transform;
        public Action<DominoTileView> Clicked;
        public Func<DominoTileView, bool> DragRequested;
        public Action<DominoTileView, PointerEventData> DragMoved;
        public Action<DominoTileView, PointerEventData> DragReleased;
        public bool IsDragging { get; private set; }
        Vector2 dragOffset;
        int dragPointer;
        public bool IsFaceUp => faceUp;
        public Vector2 Home { get; set; }
        public bool Selected { get; private set; }
        public bool Selectable { get; set; }
        public void Initialize(DominoTile tile, bool visible, bool isVertical)
        {
            Tile = tile; faceUp = visible; vertical = isVertical;
            // Existing serialized prefabs do not acquire newly required components retroactively.
            if (!GetComponent<CanvasRenderer>()) gameObject.AddComponent<CanvasRenderer>();
            if (!graphic) graphic = GetComponent<DominoFace>() ?? gameObject.AddComponent<DominoFace>();
            graphic.raycastTarget = true;
            Rect.sizeDelta = new Vector2(96, 46);
            Rect.localRotation = Quaternion.Euler(0, 0, vertical ? 90 : 0);
            if (!backBrand)
            {
                backBrand = UiKit.Label("TEAMFHO", transform, "TEAMFHO", new Vector2(86,20), new Vector2(0,4), 14, UiKit.Hex("A07C32"));
                backBrand.fontStyle = FontStyle.BoldAndItalic;
                backLogo = UiKit.Panel("TeamFHO logo",transform,new Vector2(86,25),new Vector2(0,1),Color.white);
                backLogo.sprite = TileStyles.BrandLogo;
                backLogo.type = Image.Type.Simple;
                backLogo.preserveAspect = true;
            }
            Refresh();
        }
        void Start() { if (!graphic) Initialize(new DominoTile(previewA, previewB), faceUp, vertical); }
        public void Reveal() { faceUp = true; Refresh(); }
        public void Conceal() { faceUp = false; Refresh(); }
        public void Orient(DominoTile tile) { Tile = tile; Refresh(); }
        public void Select(bool selected) { Selected = selected; Refresh(); }
        void Refresh()
        {
            if (!graphic) return;
            graphic.Set(Tile.SideA, Tile.SideB, faceUp, Selected);
            if (backBrand) backBrand.gameObject.SetActive(!faceUp && TileStyles.Current == TileStyle.TeamFhoIvory);
            if (backLogo) backLogo.gameObject.SetActive(!faceUp && TileStyles.Current == TileStyle.TeamFhoWhite);
        }
        public void OnPointerClick(PointerEventData eventData)
        { if (Selectable && !IsDragging && eventData.eligibleForClick) Clicked?.Invoke(this); }
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Selectable || IsDragging || eventData.button != PointerEventData.InputButton.Left) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)Rect.parent, eventData.position, eventData.pressEventCamera, out var pointer)) return;
            if (DragRequested == null || !DragRequested(this)) return;
            IsDragging = true;
            dragPointer = eventData.pointerId;
            dragOffset = Rect.anchoredPosition - pointer;
            eventData.eligibleForClick = false;
            transform.SetAsLastSibling();
            Rect.localScale = Vector3.one * 1.24f;
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging || eventData.pointerId != dragPointer) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)Rect.parent, eventData.position, eventData.pressEventCamera, out var pointer))
                Rect.anchoredPosition = pointer + dragOffset;
            DragMoved?.Invoke(this, eventData);
        }
        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging || eventData.pointerId != dragPointer) return;
            OnDrag(eventData);
            IsDragging = false;
            eventData.eligibleForClick = false;
            DragReleased?.Invoke(this, eventData);
        }
        public void CancelDrag() { IsDragging = false; }
    }

    // One mesh per tile, including rounded silhouette, shadow and all pips; no sprite files needed.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DominoFace : MaskableGraphic
    {
        int a, b;
        bool front, selected;
        public void Set(int sideA, int sideB, bool visible, bool highlight)
        { a = sideA; b = sideB; front = visible; selected = highlight; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            bool ivory = TileStyles.Current == TileStyle.TeamFhoIvory;
            bool brandedWhite = TileStyles.Current == TileStyle.TeamFhoWhite;
            Rounded(vh, new Rect(r.x + 2, r.y - (selected ? 7 : 4), r.width + (selected ? 2 : 0), r.height), 6, new Color(0, 0, 0, selected ? DominoVisualTheme.SelectedShadowAlpha : DominoVisualTheme.ContactShadowAlpha));
            if (selected)
            {
                Rounded(vh, new Rect(r.x - 7, r.y - 7, r.width + 14, r.height + 14), 10, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, .18f));
                Rounded(vh, new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), 8, UiKit.Gold);
            }
            Rounded(vh, r, 6, UiKit.Hex(brandedWhite ? "B2BAC8" : ivory ? "B8AD92" : "073DAE"));
            Rounded(vh, new Rect(r.x + 1, r.y + 2, r.width - 2, r.height - 3), 5, UiKit.Hex(ivory || brandedWhite ? "FFFFFF" : "76C9FF"));
            Rounded(vh, new Rect(r.x + 2, r.y + 3, r.width - 4, r.height - 5), 4,
                brandedWhite ? Color.white : ivory ? DominoVisualTheme.TileFace : front ? UiKit.Hex("057AE8") : UiKit.Hex("EBF7FF"));
            Rounded(vh, new Rect(r.x + 4, r.y + 4, r.width - 8, 2), 1,
                brandedWhite ? UiKit.Hex("E2E7EF") : ivory ? UiKit.Hex("D9D0B7") : front ? UiKit.Hex("0755C9") : UiKit.Hex("BDDEEF"));
            if (front)
            {
                var dividerColor = UiKit.Hex("171B1A");
                Quad(vh, new Rect(-.65f, 1 - r.height * .36f, 1.3f, r.height * .72f), dividerColor);
                Circle(vh, new Vector2(0, 1), 2.2f, dividerColor);
                Pips(vh, a, new Vector2(-r.width * .25f, 1), r.height);
                Pips(vh, b, new Vector2(r.width * .25f, 1), r.height);
            }
            else if (!brandedWhite)
            {
                // Cuban flag inlaid in the white reverse: five stripes, red triangle, white star.
                var flag = ivory ? new Rect(-12,-17,24,10) : new Rect(-34, -15, 68, 32);
                if (ivory)
                {
                    var gold = UiKit.Hex("AE8D45");
                    Star(vh,new Vector2(0,17),2.8f,gold);
                    Quad(vh,new Rect(-28,16.5f,20,.8f),gold);
                    Quad(vh,new Rect(8,16.5f,20,.8f),gold);
                    Quad(vh,new Rect(-31,-12.5f,15,.8f),gold);
                    Quad(vh,new Rect(16,-12.5f,15,.8f),gold);
                }
                Quad(vh, flag, UiKit.Hex("FFFFFF"));
                for (int stripe = 0; stripe < 5; stripe += 2)
                    Quad(vh, new Rect(flag.x, flag.y + stripe * flag.height / 5, flag.width, flag.height / 5), UiKit.Hex("075ACA"));
                Triangle(vh, new Vector2(flag.x, flag.y), new Vector2(flag.x + flag.height*.875f, flag.center.y),
                    new Vector2(flag.x, flag.yMax), UiKit.Hex("D72D42"));
                Star(vh, new Vector2(flag.x + flag.height*.3125f, flag.center.y), flag.height*.175f, Color.white);
            }
            // A narrow reflection gives the resin surface depth without obscuring the values.
            Rounded(vh, new Rect(r.x + 7, r.yMax - 5, r.width - 14, 1.2f), .6f, new Color(1,1,1,.6f));
        }
        static void Pips(VertexHelper vh, int n, Vector2 c, float h)
        {
            float s = h * .235f, radius = h * .077f;
            if ((n & 1) == 1) Pip(vh, c, radius);
            if (n >= 2) { Pip(vh, c + new Vector2(-s, s), radius); Pip(vh, c + new Vector2(s, -s), radius); }
            if (n >= 4) { Pip(vh, c + new Vector2(s, s), radius); Pip(vh, c + new Vector2(-s, -s), radius); }
            if (n >= 6) { Pip(vh, c + new Vector2(0, -s), radius); Pip(vh, c + new Vector2(0, s), radius); }
            if (n >= 8) { Pip(vh, c + new Vector2(-s, 0), radius); Pip(vh, c + new Vector2(s, 0), radius); }
        }
        static void Pip(VertexHelper vh, Vector2 center, float radius)
        {
            if (TileStyles.Current != TileStyle.CubaBlue)
            {
                Circle(vh,center,radius+.5f,UiKit.Hex("C1BBA9"));
                Circle(vh,center,radius,DominoVisualTheme.Pip);
                Circle(vh,center+new Vector2(-.65f,.9f),radius*.3f,UiKit.Hex("627074"));
                return;
            }
            Circle(vh, center, radius + .6f, UiKit.Hex("0749A7"));
            Circle(vh, center + new Vector2(0,-.35f), radius, UiKit.Hex("BDEBFF"));
            Circle(vh, center + new Vector2(0,.2f), radius * .8f, Color.white);
        }
        static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a,color,Vector2.zero); vh.AddVert(b,color,Vector2.zero); vh.AddVert(c,color,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2);
        }
        static void Star(VertexHelper vh, Vector2 center, float radius, Color color)
        {
            for (int i = 0; i < 10; i++)
            {
                float a = (90 + i * 36) * Mathf.Deg2Rad;
                float b = (90 + (i+1) * 36) * Mathf.Deg2Rad;
                var p = center + new Vector2(Mathf.Cos(a),Mathf.Sin(a)) * radius * (i%2 == 0 ? 1 : .4f);
                var q = center + new Vector2(Mathf.Cos(b),Mathf.Sin(b)) * radius * (i%2 == 0 ? .4f : 1);
                Triangle(vh,center,p,q,color);
            }
        }
        static void Quad(VertexHelper vh, Rect r, Color color) => Rounded(vh, r, 0, color);
        static void Circle(VertexHelper vh, Vector2 center, float radius, Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);
            const int steps = 16;
            for (int i = 0; i <= steps; i++)
            {
                float angle = i * Mathf.PI * 2 / steps;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                if (i > 0) vh.AddTriangle(start, start + i, start + i + 1);
            }
        }
        static void Rounded(VertexHelper vh, Rect r, float radius, Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(r.center, color, Vector2.zero);
            for (int corner = 0; corner < 4; corner++)
            {
                float cx = corner == 0 || corner == 3 ? r.xMax - radius : r.xMin + radius;
                float cy = corner < 2 ? r.yMax - radius : r.yMin + radius;
                for (int j = 0; j <= 5; j++)
                {
                    float angle = (corner * 90 + j * 18) * Mathf.Deg2Rad;
                    vh.AddVert(new Vector2(cx + Mathf.Cos(angle) * radius, cy + Mathf.Sin(angle) * radius), color, Vector2.zero);
                }
            }
            for (int i = 0; i < 24; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 24);
        }
    }
}
