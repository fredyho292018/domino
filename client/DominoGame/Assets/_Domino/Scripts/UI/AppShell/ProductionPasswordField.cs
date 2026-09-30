using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.UI.AppShell
{
    // One visual surface; the text editor and visibility hit target remain transparent.
    public sealed class ProductionPasswordField : VisualElement
    {
        public TextField Field { get; }
        public Button Toggle { get; }
        public Image Icon { get; }
        bool focused, invalid;
        public ProductionPasswordField(string fieldName,string title)
        {
            name=fieldName+"Row";var theme=ThemeProvider.Current;
            style.flexDirection=FlexDirection.Row;style.alignItems=Align.Center;style.flexShrink=0;
            style.height=style.minHeight=48;style.marginBottom=12;style.backgroundColor=theme.Colors.Surface;
            ThemeStyles.Round(this,theme.Radius.Input);ThemeStyles.Border(this,Color.clear,2);
            Field=new TextField{name=fieldName,isPasswordField=true};ThemeStyles.Text(Field,TextRole.Body);
            Field.style.flexGrow=1;Field.style.flexShrink=1;Field.style.minWidth=0;Field.style.height=44;
            Field.style.marginLeft=Field.style.marginRight=Field.style.marginTop=Field.style.marginBottom=0;
            Transparent(Field);
            Field.style.unityTextAlign=TextAnchor.MiddleLeft;
            Field.style.paddingTop=Field.style.paddingBottom=0;
            var input=Field.Q(className:"unity-text-field__input");
            if(input!=null){Transparent(input);input.style.color=theme.Colors.TextPrimary;input.style.paddingLeft=12;input.style.paddingRight=4;input.style.overflow=Overflow.Hidden;
                input.style.paddingTop=input.style.paddingBottom=0;
                input.style.height=Length.Percent(100);input.style.flexGrow=1;
                input.style.unityTextAlign=TextAnchor.MiddleLeft;
                input.style.justifyContent=Justify.Center;}
            var text=Field.Q<TextElement>(className:"unity-text-element");
            if(text!=null){text.style.height=Length.Percent(100);text.style.flexGrow=1;text.style.unityTextAlign=TextAnchor.MiddleLeft;}
            Add(Field);
            Icon=new Image{name="PasswordVisibilityIcon",scaleMode=ScaleMode.ScaleToFit,tintColor=theme.Colors.TextPrimary,pickingMode=PickingMode.Ignore};Icon.style.width=Icon.style.height=22;
            Toggle=new Button{name="Toggle"+fieldName};Transparent(Toggle);
            Toggle.style.width=Toggle.style.height=Toggle.style.minWidth=Toggle.style.minHeight=44;Toggle.style.flexShrink=0;
            Toggle.style.marginLeft=Toggle.style.marginTop=Toggle.style.marginBottom=0;Toggle.style.marginRight=0;
            Toggle.style.alignItems=Align.Center;Toggle.style.justifyContent=Justify.Center;
            void UpdateIcon(){Icon.vectorImage=Resources.Load<VectorImage>("AuthIcons/"+(Field.isPasswordField?"icon_eye_off":"icon_eye"));Toggle.tooltip=(Field.isPasswordField?"Show ":"Hide ")+title;}
            Toggle.clicked+=()=>{Field.isPasswordField=!Field.isPasswordField;UpdateIcon();};Toggle.Add(Icon);Add(Toggle);UpdateIcon();
            RegisterCallback<FocusInEvent>(_=>{focused=true;Paint();});
            RegisterCallback<FocusOutEvent>(e=>{focused=e.relatedTarget is VisualElement next&&Contains(next);Paint();});
            Toggle.RegisterCallback<PointerEnterEvent>(_=>Icon.style.opacity=.75f);Toggle.RegisterCallback<PointerLeaveEvent>(_=>Icon.style.opacity=1);
        }
        static void Transparent(VisualElement element){element.style.backgroundColor=Color.clear;element.style.backgroundImage=StyleKeyword.None;ThemeStyles.Border(element,Color.clear,0);ThemeStyles.Round(element,0);}
        public void SetError(bool value){invalid=value;Paint();}
        void Paint(){var colors=ThemeProvider.Current.Colors;ThemeStyles.Border(this,invalid?colors.Error:focused?colors.Primary:Color.clear,2);}
    }
}
