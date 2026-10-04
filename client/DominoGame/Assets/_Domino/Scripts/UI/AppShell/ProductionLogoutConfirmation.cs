using System;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.UI.AppShell
{
    public sealed class ProductionLogoutConfirmation : ProductionRootPage
    {
        public ProductionLogoutConfirmation(bool guest, Action cancel, Action confirm, bool busy=false, string error="", Action protect=null, string locale="en") : base("", "")
        {
            name="LogoutConfirmation";Body.Clear();verticalScrollerVisibility=ScrollerVisibility.Hidden;
            style.backgroundColor=ThemeProvider.Current.Colors.Background;
            var title=new Label(protect!=null?AccountProtectionText.Title(locale):guest?"Sign out of Guest account?":"Sign out?"){name="LogoutTitle"};
            ThemeStyles.Text(title,TextRole.PageTitle);title.style.whiteSpace=WhiteSpace.Normal;Body.Add(title);
            var warning=new Label(protect!=null?AccountProtectionText.Warning(locale):guest?"This Guest account is not linked to a sign-in method. If you sign out, you may not be able to recover this account or its progress.":"You’ll need to sign in again to continue."){name="LogoutExplanation"};
            ThemeStyles.Text(warning,TextRole.Secondary);warning.style.whiteSpace=WhiteSpace.Normal;
            warning.style.marginTop=16;warning.style.marginBottom=24;Body.Add(warning);
            var feedback=new Label(error){name="LogoutError"};ThemeStyles.Text(feedback,TextRole.Secondary);feedback.style.whiteSpace=WhiteSpace.Normal;feedback.style.color=ThemeProvider.Current.Colors.Error;Body.Add(feedback);
            if(protect!=null){var secure=new ThemeButton(AccountProtectionText.Protect(locale),protect,true){name="ProtectAccount",tooltip=AccountProtectionText.Protect(locale)};secure.style.minHeight=48;secure.SetEnabled(!busy);Body.Add(secure);}
            var no=new ThemeButton(protect!=null?AccountProtectionText.Cancel(locale):"Cancel",cancel,protect==null){name="CancelSignOut",tooltip=AccountProtectionText.Cancel(locale)};
            no.style.minHeight=48;no.SetEnabled(!busy);Body.Add(no);
            var yes=new ThemeButton(busy?"Signing out…":protect!=null?AccountProtectionText.Anyway(locale):"Sign Out",confirm){name="ConfirmSignOut",tooltip=protect!=null?AccountProtectionText.Anyway(locale):"Confirm Sign Out"};
            // Keep destructive semantics local to logout, including inherited button state refreshes.
            void DestructiveStyle(){
                var colors=ThemeProvider.Current.Colors;
                yes.style.color=busy?colors.Disabled:colors.Error;
                ThemeStyles.Border(yes,colors.Error,2);
            }
            yes.RegisterCallback<PointerEnterEvent>(_=>DestructiveStyle());yes.RegisterCallback<PointerLeaveEvent>(_=>DestructiveStyle());
            yes.RegisterCallback<PointerDownEvent>(_=>DestructiveStyle());yes.RegisterCallback<PointerUpEvent>(_=>DestructiveStyle());
            yes.RegisterCallback<PointerCancelEvent>(_=>DestructiveStyle());yes.RegisterCallback<FocusInEvent>(_=>DestructiveStyle());yes.RegisterCallback<FocusOutEvent>(_=>DestructiveStyle());
            yes.style.minHeight=48;yes.SetEnabled(!busy);DestructiveStyle();Body.Add(yes);
            if(protect!=null){no.RemoveFromHierarchy();Body.Add(no);}
        }
    }
}
