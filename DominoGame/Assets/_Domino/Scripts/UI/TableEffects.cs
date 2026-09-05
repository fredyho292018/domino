using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    /// <summary>Short, non-interactive effects driven by the presentation event queue.</summary>
    public sealed class TableEffects : MonoBehaviour
    {
        RectTransform root;
        GameObject active;
        public void Initialize(RectTransform parent) => root = UiKit.Rect("Table effects", parent, Vector2.zero, Vector2.zero);
        public void Clear()
        {
            if (!active) return;
            active.SetActive(false); Destroy(active); active = null;
        }
        RectTransform Begin(string name, Vector2 position)
        {
            Clear();
            var effect = UiKit.Rect(name, root, Vector2.zero, position);
            active = effect.gameObject;
            var group = active.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            return effect;
        }
        public IEnumerator Knock(int player)
        {
            Vector2[] positions = { new(0,-145), new(-420,0), new(0,145), new(420,0) };
            float[] angles = { -12, -90, 168, 90 };
            var effect = Begin("Pass - two knocks", positions[player]);
            var ripple = UiKit.Rect("Impact", effect, new Vector2(64,34), new Vector2(0,-13)).gameObject.AddComponent<ImpactRing>();
            ripple.color = new Color(1, .87f, .58f, 0);
            var hand = UiKit.Rect("Knocking hand", effect, new Vector2(80,90), Vector2.zero);
            hand.localRotation = Quaternion.Euler(0,0,angles[player]);
            UiKit.Panel("Shadow", hand, new Vector2(68,39), new Vector2(3,-12), new Color(0,0,0,.22f));
            UiKit.Panel("Sleeve", hand, new Vector2(37,28), new Vector2(0,-37), UiKit.Hex("E6E0CF"));
            UiKit.Panel("Wrist", hand, new Vector2(33,25), new Vector2(0,-22), UiKit.Hex("BC865F"));
            UiKit.Panel("Palm outline", hand, new Vector2(59,49), Vector2.zero, UiKit.Hex("956346"));
            UiKit.Panel("Palm", hand, new Vector2(54,44), new Vector2(-1,2), UiKit.Hex("D6A57B"));
            for (int i = 0; i < 4; i++)
            {
                float x = -20 + i*13;
                UiKit.Panel("Knuckle", hand, new Vector2(15,29), new Vector2(x,20-Mathf.Abs(i-1.5f)*3), UiKit.Hex("E7B98F"));
                UiKit.Panel("Finger crease", hand, new Vector2(7,1.5f), new Vector2(x,15), UiKit.Hex("B7835E"));
            }
            var thumb = UiKit.Panel("Thumb", hand, new Vector2(22,37), new Vector2(26,-1), UiKit.Hex("E0AC80"));
            thumb.rectTransform.localRotation = Quaternion.Euler(0,0,-28);
            UiKit.Label("Pass", effect, "PASO", new Vector2(120,26), new Vector2(0,-77), 18, UiKit.Gold);
            var group = effect.GetComponent<CanvasGroup>();
            for (int tap = 0; tap < 2; tap++)
            {
                float elapsed = 0;
                while (elapsed < .42f)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed/.42f);
                    // Contact at 38% of each cycle, followed by an expanding ripple and recoil.
                    float height = t < .38f ? Mathf.Lerp(25,0,t/.38f) : Mathf.Lerp(0,25,(t-.38f)/.62f);
                    hand.anchoredPosition = Vector2.up*height;
                    hand.localScale = Vector3.one*(1-height*.002f);
                    float impact = Mathf.Clamp01((t-.38f)/.62f);
                    ripple.rectTransform.localScale = Vector3.one*Mathf.Lerp(.7f,2.3f,impact);
                    ripple.color = new Color(1,.87f,.58f,t < .38f ? 0 : (1-impact)*.85f);
                    group.alpha = tap == 0 ? Mathf.Min(1,t*6) : 1;
                    yield return null;
                }
            }
            for (float t = 0; t < .2f; t += Time.deltaTime) { group.alpha = 1-t/.2f; yield return null; }
            Clear();
        }
        public IEnumerator Celebrate(string winner, bool match)
        {
            var effect = Begin("Winner celebration", Vector2.zero);
            var group = effect.GetComponent<CanvasGroup>();
            var card = UiKit.Panel("Winner card", effect, new Vector2(530,145), Vector2.zero, UiKit.Hex("123B34"));
            UiKit.Panel("Gold rule", card.transform, new Vector2(90,3), new Vector2(0,48), UiKit.Gold);
            UiKit.Label("Title", card.transform, match ? "¡PARTIDA GANADA!" : "¡RONDA GANADA!", new Vector2(490,40), new Vector2(0,15), 29, UiKit.Gold);
            UiKit.Label("Winner", card.transform, winner, new Vector2(490,38), new Vector2(0,-32), 25, UiKit.Cream);
            const int count = 32;
            var pieces = new RectTransform[count];
            for (int i = 0; i < count; i++)
                pieces[i] = UiKit.Panel("Confetti", effect, new Vector2(5+i%3*2,12), Vector2.zero,
                    i%3 == 0 ? UiKit.Cream : i%3 == 1 ? UiKit.Gold : UiKit.Hex("74B6A1")).rectTransform;
            float duration = match ? 2.2f : 1.6f;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
            {
                group.alpha = Mathf.Min(Mathf.Clamp01(elapsed/.15f), Mathf.Clamp01((duration-elapsed)/.3f));
                card.rectTransform.localScale = Vector3.one*(1+.07f*Mathf.Sin(Mathf.Clamp01(elapsed/.35f)*Mathf.PI));
                for (int i = 0; i < count; i++)
                {
                    float angle = (i*137.5f)*Mathf.Deg2Rad;
                    float speed = 130+i%7*18;
                    pieces[i].anchoredPosition = new Vector2(Mathf.Cos(angle)*speed*elapsed, 60+Mathf.Sin(angle)*speed*elapsed-70*elapsed*elapsed);
                    pieces[i].localRotation = Quaternion.Euler(0,0,i*23+elapsed*(90+i*7));
                }
                yield return null;
            }
            Clear();
        }
        static RectTransform OpenHand(Transform parent, bool upper, bool right)
        {
            var hand = UiKit.Rect("Open mixing hand", parent, new Vector2(90,140), Vector2.zero);
            hand.localRotation = Quaternion.Euler(0,0,upper ? 180 : 0);
            var skin = UiKit.Hex(upper ? "BC855F" : "E2B38A");
            UiKit.Panel("Sleeve",hand,new Vector2(43,55),new Vector2(0,-63),UiKit.Hex(upper ? "52786D" : "E6E0CF"));
            UiKit.Panel("Wrist",hand,new Vector2(37,32),new Vector2(0,-29),skin);
            UiKit.Panel("Palm shadow",hand,new Vector2(67,66),new Vector2(3,-3),new Color(0,0,0,.19f));
            UiKit.Panel("Palm",hand,new Vector2(61,65),Vector2.zero,skin);
            for (int i=0;i<4;i++)
            {
                float length = 47-Mathf.Abs(i-1.5f)*8;
                var finger = UiKit.Panel("Finger",hand,new Vector2(13,length),new Vector2(-24+i*16,34+length*.27f),skin);
                finger.rectTransform.localRotation = Quaternion.Euler(0,0,(1.5f-i)*5);
                UiKit.Panel("Crease",finger.transform,new Vector2(7,1),new Vector2(0,-4),new Color(.32f,.2f,.13f,.3f));
            }
            var thumb = UiKit.Panel("Thumb",hand,new Vector2(19,43),new Vector2(right ? 35 : -35,-2),skin);
            thumb.rectTransform.localRotation = Quaternion.Euler(0,0,right ? -35 : 35);
            hand.localScale = Vector3.one*.85f;
            return hand;
        }
        public IEnumerator Wash(IReadOnlyList<DominoTileView> tiles)
        {
            var effect = Begin("Darle agua al domino",Vector2.zero);
            var starts = new Vector2[tiles.Count];
            var scales = new Vector3[tiles.Count];
            var rotations = new Quaternion[tiles.Count];
            var anchors = new Vector2[tiles.Count];
            for (int i=0;i<tiles.Count;i++)
            {
                starts[i]=tiles[i].Rect.anchoredPosition;
                scales[i]=tiles[i].Rect.localScale;
                rotations[i]=tiles[i].Rect.localRotation;
                anchors[i]=new Vector2((i%11-5)*48+Mathf.Sin(i*7.3f)*13,(i/11-2)*36+Mathf.Cos(i*3.7f)*10);
            }
            // Flip every tile before gathering them, including the fifteen reserved tiles.
            for (int phase=0;phase<2;phase++)
            {
                for (float elapsed=0;elapsed<.15f;elapsed+=Time.deltaTime)
                {
                    float t=Mathf.Clamp01(elapsed/.15f);
                    float width=phase==0 ? Mathf.Lerp(1,.02f,t) : Mathf.Lerp(.02f,1,t);
                    for (int i=0;i<tiles.Count;i++) tiles[i].Rect.localScale=new Vector3(scales[i].x*width,scales[i].y,1);
                    yield return null;
                }
                if (phase==0) foreach(var tile in tiles) tile.Conceal();
            }
            for(float elapsed=0;elapsed<.65f;elapsed+=Time.deltaTime)
            {
                float t=Mathf.SmoothStep(0,1,elapsed/.65f);
                for(int i=0;i<tiles.Count;i++)
                {
                    tiles[i].Rect.anchoredPosition=Vector2.Lerp(starts[i],anchors[i],t);
                    tiles[i].Rect.localScale=Vector3.Lerp(scales[i],Vector3.one*.5f,t);
                    tiles[i].Rect.localRotation=Quaternion.Slerp(rotations[i],Quaternion.Euler(0,0,i*137.5f),t);
                }
                yield return null;
            }
            var hands=new RectTransform[4];
            for(int i=0;i<4;i++) hands[i]=OpenHand(effect,i>=2,i%2==1);
            const float duration=3.6f;
            for(float elapsed=0;elapsed<duration;elapsed+=Time.deltaTime)
            {
                float wave=elapsed*Mathf.PI*2;
                float strength=Mathf.Min(Mathf.Clamp01(elapsed/.35f),Mathf.Clamp01((duration-elapsed)/.45f));
                for(int i=0;i<4;i++)
                {
                    bool upper=i>=2;
                    float phase=wave+(i%2)*Mathf.PI+(upper ? Mathf.PI*.5f : 0);
                    hands[i].anchoredPosition=new Vector2((i%2==0 ? -105 : 105)+Mathf.Sin(phase)*75,
                        (upper ? 1 : -1)*(205-105*strength)+Mathf.Cos(phase)*22*strength);
                    hands[i].localRotation=Quaternion.Euler(0,0,(upper ? 180 : 0)+Mathf.Sin(phase)*22*strength);
                }
                for(int i=0;i<tiles.Count;i++)
                {
                    float phase=wave+i*2.39996f;
                    var swirl=new Vector2(Mathf.Sin(phase)*57,Mathf.Cos(phase*.83f)*35);
                    tiles[i].Rect.anchoredPosition=anchors[i]+swirl*strength;
                    tiles[i].Rect.localRotation=Quaternion.Euler(0,0,i*137.5f+elapsed*90*(i%2==0?1:-1));
                    tiles[i].Rect.localScale=Vector3.one*.5f;
                }
                effect.GetComponent<CanvasGroup>().alpha=strength;
                yield return null;
            }
            Clear(); // The real tiles remain face down on the table until New Match.
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ImpactRing : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var half = rectTransform.rect.size*.5f;
            for (int i = 0; i <= 48; i++)
            {
                float angle = i*Mathf.PI*2/48;
                var p = new Vector2(Mathf.Cos(angle)*half.x, Mathf.Sin(angle)*half.y);
                vh.AddVert(p,color,Vector2.zero); vh.AddVert(p*.90f,color,Vector2.zero);
                if (i == 0) continue;
                int k = i*2;
                vh.AddTriangle(k-2,k,k-1); vh.AddTriangle(k-1,k,k+1);
            }
        }
    }
}
