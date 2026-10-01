using System;
using Domino.Identity;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.UI.AppShell
{
    public enum AuthStatusVariant { Info, Loading, Success, Warning, Error }
    // Presentation only: accepts existing safe messages, never routes or invokes authentication.
    public sealed class AuthStatusMessage : VisualElement
    {
        public AuthStatusVariant Variant { get; private set; }
        public Image Icon { get; }
        public Label Title { get; }
        public Label Message { get; }
        readonly IVisualElementScheduledItem animation;float angle;
        public AuthStatusMessage()
        {
            name="AuthStatusMessage";style.flexDirection=FlexDirection.Row;style.alignItems=Align.FlexStart;style.flexShrink=0;style.marginTop=4;style.marginBottom=12;
            ThemeStyles.Pad(this,12);ThemeStyles.Round(this,ThemeProvider.Current.Radius.Input);
            Icon=new Image{name="StatusIcon",scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};Icon.style.width=Icon.style.height=20;Icon.style.flexShrink=0;Icon.style.marginRight=8;Icon.style.marginTop=1;Add(Icon);
            var content=new VisualElement();content.style.flexGrow=1;content.style.flexShrink=1;content.style.minWidth=0;Add(content);
            Title=new Label{name="StatusTitle"};ThemeStyles.Text(Title,TextRole.ButtonSecondary);Title.style.whiteSpace=WhiteSpace.Normal;content.Add(Title);
            Message=new Label{name="EmailStatus"};ThemeStyles.Text(Message,TextRole.Secondary);Message.style.whiteSpace=WhiteSpace.Normal;content.Add(Message);
            animation=schedule.Execute(()=>{angle=(angle+24)%360;Icon.style.rotate=new Rotate(new Angle(angle,AngleUnit.Degree));}).Every(80);animation.Pause();Hide();
        }
        public void Hide(){style.display=DisplayStyle.None;Title.text=Message.text=tooltip="";animation.Pause();}
        // Caller supplies curated presentation copy, never raw exception text or identity data.
        public void PresentSemantic(AuthStatusVariant variant,string safeMessage){Show(variant,"",safeMessage);}
        void Show(AuthStatusVariant variant,string title,string message)
        {
            Variant=variant;style.display=DisplayStyle.Flex;Title.text=title;Title.style.display=string.IsNullOrEmpty(title)?DisplayStyle.None:DisplayStyle.Flex;Message.text=message;Message.style.display=string.IsNullOrEmpty(message)?DisplayStyle.None:DisplayStyle.Flex;tooltip=(title+" "+message).Trim();
            var colors=ThemeProvider.Current.Colors;var color=variant==AuthStatusVariant.Error?colors.Error:variant==AuthStatusVariant.Warning?colors.Warning:variant==AuthStatusVariant.Success?colors.Success:colors.Info;
            style.backgroundColor=Color.Lerp(colors.Background,color,.09f);ThemeStyles.Border(this,Color.Lerp(colors.Background,color,.3f),1);
            Title.style.color=color;Message.style.color=variant==AuthStatusVariant.Info?colors.TextSecondary:color;Icon.tintColor=color;
            var asset=variant==AuthStatusVariant.Loading?"spinner":variant==AuthStatusVariant.Success?"success":variant==AuthStatusVariant.Warning?"warning":variant==AuthStatusVariant.Error?"error":"info";
            Icon.vectorImage=Resources.Load<VectorImage>("AuthIcons/status_"+asset);Icon.style.rotate=new Rotate(new Angle(0,AngleUnit.Degree));
            if(variant==AuthStatusVariant.Loading)animation.Resume();else animation.Pause();
        }
        public void Present(bool busy,EmailOperationState operation,string safeMessage,EmailAuthError error=EmailAuthError.None)
        {
            if(busy){Show(AuthStatusVariant.Loading,"",operation==EmailOperationState.SigningIn?"Signing you in…":operation==EmailOperationState.Resetting?"Sending reset instructions…":operation==EmailOperationState.Checking?"Checking verification…":operation==EmailOperationState.Resending?"Sending verification email…":"Creating your account…");return;}
            if(error==EmailAuthError.None){foreach(EmailAuthError known in Enum.GetValues(typeof(EmailAuthError)))if(known!=EmailAuthError.None&&safeMessage==EmailAuthRules.Message(known)){error=known;break;}}
            if(error!=EmailAuthError.None){
                if(error==EmailAuthError.PasswordMismatch)Show(AuthStatusVariant.Error,"Passwords don't match","Enter the same password in both fields.");
                else if(error==EmailAuthError.EmailAlreadyInUse)Show(AuthStatusVariant.Error,"Email already registered","Sign in or reset your password.");
                else if(error==EmailAuthError.NetworkError)Show(AuthStatusVariant.Error,"Connection problem","Check your connection and try again.");
                else if(error==EmailAuthError.InvalidCredential)Show(AuthStatusVariant.Error,"Unable to sign in",EmailAuthRules.Message(error));
                else Show(AuthStatusVariant.Error,"",EmailAuthRules.Message(error));return;
            }
            if(string.IsNullOrEmpty(safeMessage)){Hide();return;}
            switch(safeMessage){
                case EmailAuthRules.ResetSuccess:Show(AuthStatusVariant.Success,"Check your email",EmailAuthRules.ResetSuccess);break;
                case "Your email is not verified yet. Check your inbox and try again.":Show(AuthStatusVariant.Warning,"Not verified yet","Check your inbox and try again.");break;
                case "Verification email sent.":Show(AuthStatusVariant.Success,"Verification email sent","");break;
                case "Verify your email to continue.":Show(AuthStatusVariant.Info,"","Verify your email to continue.");break;
                case "This existing session cannot be changed here. Continue verifying your email.":Show(AuthStatusVariant.Info,"",safeMessage);break;
                case "Coming Soon":Show(AuthStatusVariant.Info,"","Coming Soon");break;
                // Unknown strings are not reflected: raw errors or user-entered secrets must never become status text.
                default:Show(AuthStatusVariant.Error,"",EmailAuthRules.Message(EmailAuthError.Unknown));break;
            }
        }
    }
}
