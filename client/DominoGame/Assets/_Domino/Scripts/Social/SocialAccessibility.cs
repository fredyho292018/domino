using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Accessibility;
using UnityEngine.UI;

namespace Domino.Social
{
    // Uses Unity's native accessibility hierarchy; scoped to the Social overlay only.
    public sealed class SocialAccessibility : MonoBehaviour
    {
        readonly Dictionary<Component,AccessibilityNode> nodes=new Dictionary<Component,AccessibilityNode>();
        AccessibilityHierarchy hierarchy,previous;string announcement;
        public int NodeCount=>nodes.Count;
        static Rect Frame(RectTransform rect){var corners=new Vector3[4];rect.GetWorldCorners(corners);return new Rect(corners[0].x,corners[0].y,corners[2].x-corners[0].x,corners[2].y-corners[0].y);}
        static bool Visible(Component component) {
            if(!component.gameObject.activeInHierarchy)return false;
            var rect=Frame((RectTransform)component.transform);
            foreach(var clip in component.GetComponentsInParent<RectMask2D>())if(!Frame(clip.rectTransform).Overlaps(rect))return false;
            return new Rect(0,0,Screen.width,Screen.height).Overlaps(rect);
        }
        void LateUpdate() {
            if(hierarchy==null){previous=AssistiveSupport.activeHierarchy;hierarchy=new AccessibilityHierarchy();AssistiveSupport.activeHierarchy=hierarchy;}
            var components=new List<Component>();
            components.AddRange(GetComponentsInChildren<Button>().Where(Visible));
            components.AddRange(GetComponentsInChildren<InputField>().Where(Visible));
            components.AddRange(GetComponentsInChildren<Text>().Where(t=>Visible(t)&&t.isActiveAndEnabled&&!t.GetComponentInParent<Button>()&&!t.GetComponentInParent<InputField>()&&!string.IsNullOrEmpty(t.text)));
            foreach(var old in nodes.Keys.Where(k=>!k||!components.Contains(k)).ToArray()){hierarchy.RemoveNode(nodes[old]);nodes.Remove(old);}
            foreach(var component in components.OrderByDescending(c=>Frame((RectTransform)c.transform).yMax).ThenBy(c=>Frame((RectTransform)c.transform).xMin)) {
                if(!nodes.TryGetValue(component,out var node)) {
                    node=hierarchy.AddNode("");nodes.Add(component,node);var captured=component;
                    node.frameGetter=()=>captured?Frame((RectTransform)captured.transform):Rect.zero;
                    if(component is Button button){node.role=AccessibilityRole.Button;node.selected+=()=>{if(!button||!button.IsInteractable())return false;button.onClick.Invoke();return true;};}
                    else if(component is InputField field){node.role=AccessibilityRole.SearchField;node.selected+=()=>{if(!field)return false;field.ActivateInputField();return true;};}
                    else node.role=AccessibilityRole.StaticText;
                }
                if(component is Button b){node.label=b.GetComponentInChildren<Text>().text;node.state=b.IsInteractable()?AccessibilityState.None:AccessibilityState.Disabled;
                    var row=b.transform.parent.Find("Public name and code");if(row)node.label+=". "+row.GetComponent<Text>().text;}
                else if(component is InputField input){node.label=Domino.UI.DominoLocalization.Get("social.hint");node.value=input.text;}
                else if(component is Text text){node.label=text.text;if(text.name=="Status"&&announcement!=text.text){announcement=text.text;if(AssistiveSupport.isScreenReaderEnabled)AssistiveSupport.notificationDispatcher.SendAnnouncement(announcement);}}
            }
        }
        void OnDestroy(){if(AssistiveSupport.activeHierarchy==hierarchy)AssistiveSupport.activeHierarchy=previous;nodes.Clear();}
    }
}
