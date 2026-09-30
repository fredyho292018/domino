using Domino.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.AppShellMock
{
    // ModernSocialPremium palette scoped to the visual mock only.
    public static class MockShellTheme
    {
        public static readonly Color Primary=UiKit.Hex("71A84B");
        public static readonly Color Background=UiKit.Hex("2A2623");
        public static readonly Color Surface=UiKit.Hex("41403C");
        public static readonly Color Text=UiKit.Hex("FBFAFA");
        public static readonly Color Inactive=UiKit.Hex("969495");
        // Brighter secondary body copy maintains contrast on cards; inactive icons use the exact token.
        public static readonly Color SecondaryText=UiKit.Hex("BBB9BA");
        public static void Selection(VisualElement element,bool selected)
        {
            element.style.backgroundColor=Surface;
            Border(element,selected?Primary:Surface,selected?2:0);
        }
        static void Border(VisualElement e,Color color,int width)
        {
            e.style.borderTopColor=e.style.borderBottomColor=e.style.borderLeftColor=e.style.borderRightColor=color;
            e.style.borderTopWidth=e.style.borderBottomWidth=e.style.borderLeftWidth=e.style.borderRightWidth=width;
        }
        public static void StyleInput(VisualElement field)
        {
            var input=field.Q(className:"unity-base-field__input")??field;
            input.style.backgroundColor=Surface;input.style.color=Text;
            input.style.borderTopLeftRadius=input.style.borderTopRightRadius=input.style.borderBottomLeftRadius=input.style.borderBottomRightRadius=10;
            Border(input,Surface,2);
            field.RegisterCallback<FocusInEvent>(_=>Border(input,Primary,2));
            field.RegisterCallback<FocusOutEvent>(_=>Border(input,Surface,2));
        }
    }
}
