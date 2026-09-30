using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.UI.Theming
{
    // Opt-in UI Toolkit styles. Does not mutate UiKit or any existing scene.
    public static class ThemeStyles
    {
        public static void Round(VisualElement e,float radius){e.style.borderTopLeftRadius=e.style.borderTopRightRadius=e.style.borderBottomLeftRadius=e.style.borderBottomRightRadius=radius;}
        public static void Pad(VisualElement e,float padding){e.style.paddingLeft=e.style.paddingRight=e.style.paddingTop=e.style.paddingBottom=padding;}
        public static void Border(VisualElement e,Color color,float width){e.style.borderTopColor=e.style.borderBottomColor=e.style.borderLeftColor=e.style.borderRightColor=color;e.style.borderTopWidth=e.style.borderBottomWidth=e.style.borderLeftWidth=e.style.borderRightWidth=width;}
        public static void Text(VisualElement e,TextRole role)
        {
            var t=ThemeProvider.Current;var token=t.Typography[role];e.style.fontSize=token.Size;e.style.unityFont=t.Typography.FontFor(role);
            e.style.color=role==TextRole.Secondary||role==TextRole.Caption?t.Colors.TextSecondary:t.Colors.TextPrimary;
        }
        public static void Page(VisualElement e){var t=ThemeProvider.Current;e.style.backgroundColor=t.Colors.Background;e.style.color=t.Colors.TextPrimary;e.style.width=Length.Percent(100);e.style.maxWidth=t.Sizing.ContentMaxWidth;e.style.alignSelf=Align.Center;Pad(e,t.Spacing.PageMargin);}
        public static void Card(VisualElement e,bool selected=false){var t=ThemeProvider.Current;e.style.backgroundColor=t.Colors.Surface;Round(e,t.Radius.Card);Pad(e,t.Spacing.CardPadding);Border(e,selected?t.Colors.Selected:Color.clear,selected?2:0);}
        public static void Icon(Image e,bool active=true){var t=ThemeProvider.Current;e.style.width=e.style.height=t.Sizing.NavIconLogicalSize;e.style.flexShrink=0;e.scaleMode=ScaleMode.ScaleToFit;e.tintColor=active?t.Colors.IconActive:t.Colors.IconInactive;e.pickingMode=PickingMode.Ignore;}
        public static void Back(Button root,Image icon)
        {
            var t=ThemeProvider.Current;root.tooltip="Back";root.style.minWidth=root.style.minHeight=t.Sizing.MinTouchTarget;root.style.backgroundColor=Color.clear;Border(root,Color.clear,0);
            Icon(icon);icon.vectorImage=Resources.Load<VectorImage>(t.BackIconResource);
            root.style.alignItems=Align.Center;root.style.justifyContent=Justify.Center;
        }
        public static void BottomTab(ThemeButton button,Image icon,bool selected)
        {
            var t=ThemeProvider.Current;button.style.minHeight=t.Sizing.BottomTabTouchHeight;button.style.minWidth=t.Sizing.MinTouchTarget;button.style.flexGrow=1;button.style.flexBasis=0;Text(button,TextRole.NavigationLabel);Icon(icon,selected);button.Selected=selected;
        }
        public static void Row(ThemeButton root,bool auth)
        {
            var t=ThemeProvider.Current;root.style.height=root.style.minHeight=auth?t.Sizing.AuthRowHeight:t.Sizing.MenuRowHeight;root.style.flexDirection=FlexDirection.Row;root.style.alignItems=Align.Center;root.style.flexShrink=0;root.text="";
        }
        public static void RowContent(ThemeButton root,Image icon,string title,string secondary,Image chevron,bool auth)
        {
            var t=ThemeProvider.Current;Row(root,auth);Icon(icon);var holder=new VisualElement {name="IconContainer",pickingMode=PickingMode.Ignore};holder.style.width=holder.style.height=t.Sizing.AuthIconContainer;holder.style.flexShrink=0;holder.style.alignItems=Align.Center;holder.style.justifyContent=Justify.Center;Round(holder,t.Radius.IconContainer);holder.Add(icon);root.Add(holder);
            var content=new VisualElement {pickingMode=PickingMode.Ignore};content.style.flexGrow=1;content.style.minWidth=0;content.style.marginLeft=t.Spacing.IconTextGap;
            var primary=new Label(title){name="PrimaryLabel",pickingMode=PickingMode.Ignore};Text(primary,auth?TextRole.ButtonPrimary:TextRole.ButtonSecondary);primary.style.unityTextAlign=TextAnchor.MiddleLeft;content.Add(primary);
            if(auth){var detail=new Label(secondary){name="SecondaryLabel",pickingMode=PickingMode.Ignore};Text(detail,TextRole.Caption);content.Add(detail);}root.Add(content);
            Icon(chevron,false);root.Add(chevron);root.tooltip=title;
        }
        public static void Segmented(VisualElement container){var t=ThemeProvider.Current;container.style.flexDirection=FlexDirection.Row;container.style.backgroundColor=t.Colors.Surface;Round(container,t.Radius.Segmented);Pad(container,t.Spacing.XS);}
        public static TextField Input(string label,string placeholder)
        {
            var t=ThemeProvider.Current;var field=new TextField(label);Text(field,TextRole.Body);field.style.minHeight=t.Sizing.MinTouchTarget;
            var input=field.Q(className:"unity-base-field__input");input.style.backgroundColor=t.Colors.Surface;input.style.color=t.Colors.TextPrimary;Round(input,t.Radius.Input);Border(input,t.Colors.Surface,2);
            var hint=new Label(placeholder){name="ThemePlaceholder",pickingMode=PickingMode.Ignore};Text(hint,TextRole.Secondary);hint.style.position=Position.Absolute;hint.style.left=t.Spacing.SM;hint.style.top=t.Spacing.XS;input.Add(hint);
            void UpdateHint(){hint.style.display=string.IsNullOrEmpty(field.value)?DisplayStyle.Flex:DisplayStyle.None;}
            field.RegisterValueChangedCallback(_=>UpdateHint());field.RegisterCallback<FocusInEvent>(_=>{Border(input,t.Colors.Primary,2);hint.style.display=DisplayStyle.None;});field.RegisterCallback<FocusOutEvent>(_=>{Border(input,t.Colors.Surface,2);UpdateHint();});UpdateHint();return field;
        }
    }
    public sealed class ThemeButton:Button
    {
        public AppTheme Theme {get;}=ThemeProvider.Resolve();
        readonly bool primary;bool selected,hover,pressed,focused;
        public bool Selected {get=>selected;set{selected=value;RefreshState();}}
        public ComponentState CurrentState=>!enabledInHierarchy?ComponentState.Disabled:pressed?ComponentState.Pressed:selected?ComponentState.Selected:focused?ComponentState.Focused:hover?ComponentState.Hover:ComponentState.Normal;
        public ThemeButton(string label,Action action,bool primary=false):base(action)
        {
            this.primary=primary;text=label;style.minHeight=Theme.Sizing.ButtonHeight;style.minWidth=Theme.Sizing.MinTouchTarget;style.marginBottom=Theme.Spacing.RowGap;style.paddingLeft=style.paddingRight=Theme.Spacing.MD;style.flexShrink=0;
            ThemeStyles.Round(this,Theme.Radius.Button);ThemeStyles.Text(this,primary?TextRole.ButtonPrimary:TextRole.ButtonSecondary);
            RegisterCallback<PointerEnterEvent>(_=>{hover=true;RefreshState();});RegisterCallback<PointerLeaveEvent>(_=>{hover=pressed=false;RefreshState();});
            RegisterCallback<PointerDownEvent>(_=>{pressed=true;RefreshState();});RegisterCallback<PointerUpEvent>(_=>{pressed=false;RefreshState();});RegisterCallback<PointerCancelEvent>(_=>{pressed=false;RefreshState();});
            RegisterCallback<FocusInEvent>(_=>{focused=true;RefreshState();});RegisterCallback<FocusOutEvent>(_=>{focused=pressed=false;RefreshState();});RefreshState();
        }
        // Call when an ancestor's enabled state changes; own SetEnabled handles it directly.
        public new void SetEnabled(bool value){base.SetEnabled(value);RefreshState();}
        public void RefreshState()
        {
            var c=Theme.Colors;var state=CurrentState;var bg=primary?c.Primary:c.Surface;
            style.backgroundColor=state==ComponentState.Pressed?Color.Lerp(bg,c.Background,.2f):state==ComponentState.Hover?Color.Lerp(bg,c.TextPrimary,.06f):bg;
            style.color=state==ComponentState.Disabled?c.Disabled:c.TextPrimary;style.opacity=state==ComponentState.Disabled?.55f:1;
            ThemeStyles.Border(this,selected||focused?c.Primary:Color.clear,selected||focused?2:0);
        }
    }
}
