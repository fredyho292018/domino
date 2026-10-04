using System;
using System.Collections.Generic;
using System.Linq;
using Domino.Identity;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.UI.AppShell
{
    // Retain the exact underlying view/scroll/form. Only its display is suspended.
    public sealed class ProductionAccountProtectionOverlay : IDisposable
    {
        readonly VisualElement root;
        readonly AccountProtectionController controller;
        readonly ProductionLogoutService logout;
        readonly Func<string> locale;
        readonly Func<VisualElement> protectForm;
        readonly Func<bool> canCancelForm;
        readonly Dictionary<VisualElement,StyleEnum<DisplayStyle>> hidden=new Dictionary<VisualElement,StyleEnum<DisplayStyle>>();
        VisualElement layer; Focusable priorFocus; bool disposed;
        public bool Visible=>layer!=null;
        public ProductionAccountProtectionOverlay(VisualElement root,AccountProtectionController controller,ProductionLogoutService logout,Func<string> locale,Func<VisualElement> protectForm=null,Func<bool> canCancelForm=null)
        {this.root=root;this.controller=controller;this.logout=logout;this.locale=locale;this.protectForm=protectForm;this.canCancelForm=canCancelForm;controller.Changed+=Render;logout.Changed+=Render;}
        void Render()
        {
            if(disposed)return;
            if(logout.State==LogoutState.Success){Hide();return;}
            bool progress=logout.State==LogoutState.LoggingOut||logout.State==LogoutState.Error;
            if(controller.State==AccountProtectionState.Closed&&!progress){Hide();return;}
            var language=locale();
            if(layer==null){
                priorFocus=root.focusController?.focusedElement;
                foreach(var child in root.Children().ToList()){hidden[child]=child.style.display;child.style.display=DisplayStyle.None;}
                layer=new VisualElement{name="AccountProtectionOverlay",focusable=true,tabIndex=-1};
                layer.style.position=Position.Absolute;layer.style.left=layer.style.right=layer.style.top=layer.style.bottom=0;
                layer.style.backgroundColor=ThemeProvider.Current.Colors.Background;
                layer.RegisterCallback<KeyDownEvent>(Key,TrickleDown.TrickleDown);root.Add(layer);
                layer.RegisterCallback<NavigationMoveEvent>(Navigate,TrickleDown.TrickleDown);
            }
            layer.Clear();
            if(!progress&&controller.State==AccountProtectionState.Warning)
                layer.Add(new ProductionLogoutConfirmation(true,controller.Cancel,()=>{_=controller.ForceLogoutAsync();},protect:controller.Protect,locale:language));
            else if(!progress&&controller.State==AccountProtectionState.ProtectEntry&&protectForm!=null)layer.Add(protectForm());
            else {
                var page=new EntryPage();layer.Add(page);
                var title=new Label(progress?AccountProtectionText.SigningOut(language):AccountProtectionText.Title(language)){name="ProtectionTitle"};
                ThemeStyles.Text(title,TextRole.PageTitle);title.style.whiteSpace=WhiteSpace.Normal;page.Body.Add(title);
                string message=progress?(logout.State==LogoutState.Error?AccountProtectionText.LogoutError(language):""):
                    controller.State==AccountProtectionState.ProtectEntry?AccountProtectionText.Entry(language):AccountProtectionText.Unavailable(language);
                var detail=new Label(message){name="ProtectionExplanation"};ThemeStyles.Text(detail,TextRole.Secondary);detail.style.whiteSpace=WhiteSpace.Normal;detail.style.marginTop=16;detail.style.marginBottom=24;page.Body.Add(detail);
                if(logout.State==LogoutState.Error){
                    var retry=new ThemeButton(AccountProtectionText.Retry(language),()=>{logout.Cancel();_=controller.RequestAsync();},true){name="RetryLogout"};page.Body.Add(retry);
                    page.Body.Add(new ThemeButton(AccountProtectionText.Cancel(language),logout.Cancel){name="CancelSignOut"});
                } else if(!progress)page.Body.Add(new ThemeButton(AccountProtectionText.Cancel(language),controller.Cancel){name="CancelSignOut",tooltip=AccountProtectionText.Cancel(language)});
            }
            var expectedLayer=layer;layer.schedule.Execute(()=>{if(layer==expectedLayer)Actions().FirstOrDefault()?.Focus();});
        }
        List<VisualElement> Actions()=>layer?.Q<ProductionRootPage>()?.Body.Query<VisualElement>().ToList().Where(b=>(b is Button||b is TextField)&&b.enabledInHierarchy&&b.resolvedStyle.display!=DisplayStyle.None).ToList()??new List<VisualElement>();
        void Key(KeyDownEvent evt)
        {
            if(evt.keyCode==KeyCode.Escape){evt.StopImmediatePropagation();if(controller.State==AccountProtectionState.ProtectEntry&&canCancelForm?.Invoke()==false)return;if(logout.State==LogoutState.Error)logout.Cancel();else if(logout.State!=LogoutState.LoggingOut)controller.Cancel();return;}
        }
        void Navigate(NavigationMoveEvent evt)
        {
            bool backwards=evt.direction==NavigationMoveEvent.Direction.Previous;
            if(!backwards&&evt.direction!=NavigationMoveEvent.Direction.Next)return;
            // Unity 6 translates Tab into navigation. Handle that event once, not both events.
            evt.StopImmediatePropagation();var actions=Actions();if(actions.Count==0){layer.Focus();return;}
            int index=actions.FindIndex(b=>ReferenceEquals(b,root.focusController?.focusedElement)||b is TextField&&b.Contains(root.focusController?.focusedElement as VisualElement));
            int next=index<0?(backwards?actions.Count-1:0):(index+(backwards?-1:1)+actions.Count)%actions.Count;
            actions[next].Focus();
            root.focusController?.IgnoreEvent(evt);
        }
        void Hide()
        {
            if(layer==null)return;layer.RemoveFromHierarchy();layer=null;
            foreach(var pair in hidden)pair.Key.style.display=pair.Value;hidden.Clear();
            priorFocus?.Focus();priorFocus=null;
        }
        public void Dispose(){if(disposed)return;disposed=true;controller.Changed-=Render;logout.Changed-=Render;Hide();}
        sealed class EntryPage:ProductionRootPage
        {public EntryPage():base("",""){Body.Clear();verticalScrollerVisibility=ScrollerVisibility.Hidden;style.backgroundColor=ThemeProvider.Current.Colors.Background;}}
    }
}
