using System;
using System.Collections.Generic;
using Domino.Identity;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.UI.AppShell
{
    // Presentation only. Preview supplies isolated callbacks; runtime supplies the existing router.
    public sealed class ProductionEmailView : ScrollView
    {
        public VisualElement Body { get; } = new VisualElement { name="EmailBody" };
        public TextField Email { get; private set; }
        public TextField Password { get; private set; }
        public TextField Confirmation { get; private set; }
        readonly List<Button> actions=new List<Button>();
        readonly AuthStatusMessage status;readonly VisualElement confirmation;
        readonly ThemeButton submit;readonly VisualElement recovery;
        readonly Action<ProductionAuthRoute> navigate;
        readonly Action<bool> cancel;
        public ProductionAuthRoute Route {get;}
        public ProductionEmailView(ProductionAuthRoute route,Action<ProductionAuthRoute> navigate,
            Action<string,string,string> register,Action check,Action resend,Action<bool> cancel,
            Action<string,string> signIn=null,Action<string> reset=null) : base(ScrollViewMode.Vertical)
        {
            Route=route;this.navigate=navigate;this.cancel=cancel;name="ProductionEmail";
            style.flexGrow=1;style.minHeight=0;style.backgroundColor=ThemeProvider.Current.Colors.Background;
            verticalScrollerVisibility=horizontalScrollerVisibility=ScrollerVisibility.Hidden;
            contentContainer.style.flexGrow=1;contentContainer.style.justifyContent=Justify.Center;
            ThemeStyles.Page(Body);Body.style.flexShrink=0;Add(Body);
            var back=new Button(()=>{if(route==ProductionAuthRoute.VerificationPending)ConfirmCancel();else navigate(route==ProductionAuthRoute.ForgotPassword?ProductionAuthRoute.EmailSignIn:route==ProductionAuthRoute.EmailEntry||route==ProductionAuthRoute.EmailSignIn?ProductionAuthRoute.Welcome:ProductionAuthRoute.EmailEntry);}){name="EmailBack"};
            var arrow=new Image();ThemeStyles.Back(back,arrow);back.Add(arrow);back.style.width=44;back.style.height=44;back.style.alignSelf=Align.FlexStart;back.style.marginBottom=ThemeProvider.Current.Spacing.MD;Body.Add(back);actions.Add(back);
            var heading=Text(route==ProductionAuthRoute.EmailEntry?"Email":route==ProductionAuthRoute.Register?"Create Account":route==ProductionAuthRoute.VerificationPending?"Check your email":route==ProductionAuthRoute.ForgotPassword?"Reset your password":"Sign In",TextRole.PageTitle);heading.name="EmailPageTitle";heading.style.unityTextAlign=TextAnchor.MiddleCenter;
            if(route==ProductionAuthRoute.EmailEntry){
                CenteredText("Continue with your email.",TextRole.Secondary,"EmailSubtitle");
                Action("Create Account","EmailCreate",()=>navigate(ProductionAuthRoute.Register),true);
                Action("Sign In","EmailSignIn",()=>navigate(ProductionAuthRoute.EmailSignIn));
            }else if(route==ProductionAuthRoute.Register){
                Email=Field("Email","EmailAddress",false);Email.keyboardType=TouchScreenKeyboardType.EmailAddress;
                Password=Field("Password","EmailPassword",true);Confirmation=Field("Confirm Password","EmailConfirmation",true);
                submit=Action("Create Account","EmailSubmit",()=>{
                    var error=EmailAuthRules.Validate(Email.value,Password.value,Confirmation.value);
                    SetPasswordErrors(error);
                    if(error!=EmailAuthError.None){SetStatus(EmailAuthRules.Message(error));return;}
                    var address=Email.value;var secret=Password.value;var repeated=Confirmation.value;
                    ClearPasswords();register(address,secret,repeated);
                },true);
                InlineSignIn();
                recovery=new VisualElement{name="EmailRecovery"};Body.Add(recovery);
                var forgot=Action("Forgot Password","EmailForgot",()=>navigate(ProductionAuthRoute.ForgotPassword));forgot.RemoveFromHierarchy();recovery.Add(forgot);
                var another=Action("Use Another Email","EmailAnother",()=>{Email.value="";ClearPasswords();SetStatus("");recovery.style.display=DisplayStyle.None;});another.RemoveFromHierarchy();recovery.Add(another);recovery.style.display=DisplayStyle.None;
            }else if(route==ProductionAuthRoute.EmailSignIn){
                Email=Field("Email","EmailAddress",false);Email.keyboardType=TouchScreenKeyboardType.EmailAddress;
                Password=Field("Password","EmailPassword",true);
                submit=Action("Sign In","EmailSubmit",()=>{
                    var error=EmailAuthRules.ValidateSignIn(Email.value,Password.value);SetPasswordErrors(error);
                    if(error!=EmailAuthError.None){status.Present(false,EmailOperationState.Idle,"",error);return;}
                    var address=EmailAuthRules.Normalize(Email.value);var secret=Password.value;ClearPasswords();signIn?.Invoke(address,secret);
                },true);
                InlineAction("Forgot password?","EmailForgot",()=>navigate(ProductionAuthRoute.ForgotPassword));
                InlineAccount("Don't have an account?","Create Account","SignInCreateAccountRow","EmailCreate",ProductionAuthRoute.Register);
            }else if(route==ProductionAuthRoute.ForgotPassword){
                CenteredText("Enter your email and we'll send reset instructions.",TextRole.Secondary,"EmailSubtitle");
                Email=Field("Email","EmailAddress",false);Email.keyboardType=TouchScreenKeyboardType.EmailAddress;
                submit=Action("Send Reset Link","EmailSubmit",()=>{
                    var error=EmailAuthRules.ValidateEmail(Email.value);
                    if(error!=EmailAuthError.None){status.Present(false,EmailOperationState.Idle,"",error);return;}
                    reset?.Invoke(EmailAuthRules.Normalize(Email.value));
                },true);
                InlineAction("Back to Sign In","EmailSignIn",()=>navigate(ProductionAuthRoute.EmailSignIn));
            }else if(route==ProductionAuthRoute.VerificationPending){
                CenteredText("Open the verification link in your inbox, then return here.",TextRole.Secondary,"EmailSubtitle");
                var address=CenteredText("",TextRole.Body,"VerificationAddress");address.name="VerificationAddress";
                Action("I've Verified","EmailVerified",check,true);
                Action("Resend Email","EmailResend",resend);
                Action("Use Another Email","EmailAnother",ConfirmCancel);
            }else Text("Coming Soon",TextRole.Secondary);
            status=new AuthStatusMessage();if(recovery!=null)Body.Insert(Body.IndexOf(recovery),status);else Body.Add(status);SetStatus("");
            confirmation=new VisualElement{name="EmailCancelConfirmation"};confirmation.style.display=DisplayStyle.None;Body.Add(confirmation);
            var warning=new Label("End this new unverified Email session? Your account will not be deleted.");ThemeStyles.Text(warning,TextRole.Secondary);warning.style.whiteSpace=WhiteSpace.Normal;confirmation.Add(warning);
            var yes=Action("Use Another Email","EmailConfirmCancel",()=>{confirmation.style.display=DisplayStyle.None;cancel(true);});yes.RemoveFromHierarchy();confirmation.Add(yes);
            var no=Action("Keep verifying","EmailKeepVerifying",()=>confirmation.style.display=DisplayStyle.None);no.RemoveFromHierarchy();confirmation.Add(no);
            RegisterCallback<DetachFromPanelEvent>(_=>ClearPasswords());
        }
        Label CenteredText(string value,TextRole role,string name){var label=Text(value,role);label.name=name;label.style.unityTextAlign=TextAnchor.MiddleCenter;return label;}
        void InlineSignIn(){
            InlineAccount("Already have an account?","Sign In","RegisterSignInRow","EmailSignIn",ProductionAuthRoute.EmailSignIn);
        }
        Button InlineAction(string text,string name,Action callback){
            var link=new Button(callback){name=name,text=text};var theme=ThemeProvider.Current;
            ThemeStyles.Text(link,TextRole.ButtonPrimary);link.style.fontSize=13;link.style.color=theme.Colors.Primary;
            link.style.unityTextAlign=TextAnchor.MiddleCenter;link.style.alignItems=Align.Center;link.style.justifyContent=Justify.Center;
            link.style.backgroundColor=Color.clear;link.style.backgroundImage=StyleKeyword.None;ThemeStyles.Border(link,Color.clear,0);
            link.style.marginLeft=link.style.marginRight=link.style.marginTop=link.style.marginBottom=0;
            link.style.paddingLeft=link.style.paddingRight=link.style.paddingTop=link.style.paddingBottom=0;
            link.style.minWidth=link.style.minHeight=44;link.style.flexShrink=0;Body.Add(link);actions.Add(link);return link;
        }
        void InlineAccount(string text,string action,string rowName,string linkName,ProductionAuthRoute target){
            var theme=ThemeProvider.Current;var row=new VisualElement{name="RegisterSignInRow"};row.style.flexDirection=FlexDirection.Row;row.style.justifyContent=Justify.Center;row.style.alignItems=Align.Center;row.style.flexShrink=0;row.style.marginTop=theme.Spacing.SM;
            row.name=rowName;var prompt=new Label(text);ThemeStyles.Text(prompt,TextRole.Secondary);row.Add(prompt);
            var link=new Button(()=>navigate(target)){name=linkName,text=action};
            ThemeStyles.Text(link,TextRole.ButtonPrimary);link.style.fontSize=13;link.style.color=theme.Colors.Primary;link.style.unityTextAlign=TextAnchor.MiddleCenter;
            link.style.backgroundColor=Color.clear;link.style.backgroundImage=StyleKeyword.None;ThemeStyles.Border(link,Color.clear,0);
            link.style.marginLeft=theme.Spacing.SM;link.style.marginRight=link.style.marginTop=link.style.marginBottom=0;
            link.style.paddingLeft=link.style.paddingRight=link.style.paddingTop=link.style.paddingBottom=0;
            link.style.minWidth=link.style.minHeight=44;link.style.flexShrink=0;row.Add(link);Body.Add(row);actions.Add(link);
            link.style.alignItems=Align.Center;link.style.justifyContent=Justify.Center;
        }
        bool canCancel;
        void ConfirmCancel(){if(canCancel)confirmation.style.display=DisplayStyle.Flex;else SetStatus("This existing session cannot be changed here. Continue verifying your email.");}
        Label Text(string value,TextRole role){var label=new Label(value);ThemeStyles.Text(label,role);label.style.whiteSpace=WhiteSpace.Normal;label.style.flexShrink=0;label.style.marginBottom=ThemeProvider.Current.Spacing.MD;Body.Add(label);return label;}
        ThemeButton Action(string title,string name,Action callback,bool primary=false){var button=new ThemeButton(title,callback,primary){name=name};button.style.whiteSpace=WhiteSpace.Normal;button.style.unityTextAlign=TextAnchor.MiddleCenter;Body.Add(button);actions.Add(button);return button;}
        TextField Field(string title,string name,bool secret){
            var label=Text(title,TextRole.Body);label.name=name+"Label";label.style.unityTextAlign=TextAnchor.MiddleLeft;label.style.marginBottom=ThemeProvider.Current.Spacing.SM;
            if(secret){var password=new ProductionPasswordField(name,title);Body.Add(password);actions.Add(password.Toggle);return password.Field;}
            var field=new TextField{name=name,isPasswordField=false};field.style.height=field.style.minHeight=48;field.style.marginLeft=field.style.marginRight=field.style.marginTop=0;field.style.flexShrink=0;field.style.marginBottom=12;
            ThemeStyles.Text(field,TextRole.Body);var input=field.Q(className:"unity-text-field__input");if(input!=null){input.style.backgroundColor=ThemeProvider.Current.Colors.Surface;input.style.color=ThemeProvider.Current.Colors.TextPrimary;ThemeStyles.Round(input,12);ThemeStyles.Pad(input,12);}
            Body.Add(field);
            return field;
        }
        void SetPasswordErrors(EmailAuthError error){this.Q<ProductionPasswordField>("EmailPasswordRow")?.SetError(error==EmailAuthError.WeakPassword||error==EmailAuthError.PasswordRequired||error==EmailAuthError.InvalidCredential);this.Q<ProductionPasswordField>("EmailConfirmationRow")?.SetError(error==EmailAuthError.PasswordMismatch);}
        public void ClearPasswords(){if(Password!=null)Password.SetValueWithoutNotify("");if(Confirmation!=null)Confirmation.SetValueWithoutNotify("");}
        public void PresentLinkedVerification(string locale,EmailAuthError error=EmailAuthError.None,bool busy=false,string message="")
        {
            if(Route!=ProductionAuthRoute.VerificationPending)return;
            bool es=locale=="es";
            this.Q<Label>("EmailPageTitle").text=es?"Revisa tu correo":"Check your email";
            this.Q<Label>("EmailSubtitle").text=es?"Abre el enlace de verificación de tu correo y vuelve aquí.":"Open the verification link in your inbox, then return here.";
            this.Q<Button>("EmailVerified").text=es?"Ya lo he verificado":"I've Verified";
            this.Q<Button>("EmailResend").text=es?"Reenviar correo":"Resend Email";
            // This is a linked account, not a reversible registration or anonymous session.
            this.Q<Button>("EmailAnother").text=es?"Cerrar sesión":"Sign Out";
            this.Q<Button>("EmailConfirmCancel").text=es?"Cerrar sesión":"Sign Out";
            this.Q<Button>("EmailKeepVerifying").text=es?"Seguir verificando":"Keep verifying";
            confirmation.Q<Label>().text=es?"¿Cerrar esta sesión? Tu cuenta vinculada no se eliminará.":"End this session? Your linked account will not be deleted.";
            if(busy)status.PresentSemantic(AuthStatusVariant.Loading,es?"Comprobando tu cuenta…":"Checking your account…");
            else if(error!=EmailAuthError.None)status.PresentSemantic(AuthStatusVariant.Warning,
                error==EmailAuthError.SessionConflict
                    ?(es?"Tu método de acceso ya está vinculado. No pudimos confirmar que el perfil sigue siendo el mismo. Contacta con soporte.":"Your sign-in method is already linked. We could not confirm the same profile. Please contact support.")
                    :(es?"Tu método de acceso ya está vinculado. No pudimos completar la verificación. Inténtalo de nuevo.":"Your sign-in method is already linked. We could not complete verification. Please try again."));
            else if(message=="Verification email sent.")status.PresentSemantic(AuthStatusVariant.Success,es?"Correo de verificación enviado.":"Verification email sent.");
            else status.PresentSemantic(AuthStatusVariant.Info,es?"Verifica tu correo y vuelve aquí para continuar.":"Verify your email, then return here to continue.");
        }
        public void SetStatus(string text){status.Present(false,EmailOperationState.Idle,text);}
        public void SetState(bool busy,EmailOperationState state,string message,string email,bool cancelAllowed,EmailAuthError error=EmailAuthError.None){
            canCancel=cancelAllowed;SetPasswordErrors(error);
            foreach(var action in actions){action.SetEnabled(!busy);if(action is ThemeButton themed)themed.RefreshState();}
            Email?.SetEnabled(!busy);Password?.SetEnabled(!busy);Confirmation?.SetEnabled(!busy);
            if(submit!=null){submit.text=Route==ProductionAuthRoute.EmailSignIn?(busy?"Signing in…":"Sign In"):Route==ProductionAuthRoute.ForgotPassword?(busy?"Sending…":"Send Reset Link"):(busy?"Creating account…":"Create Account");
                if(Route==ProductionAuthRoute.ForgotPassword)submit.style.display=message==EmailAuthRules.ResetSuccess?DisplayStyle.None:DisplayStyle.Flex;}
            var address=this.Q<Label>("VerificationAddress");if(address!=null)address.text=email??"";
            if(recovery!=null)recovery.style.display=error==EmailAuthError.EmailAlreadyInUse?DisplayStyle.Flex:DisplayStyle.None;
            status.Present(busy,state,message,error);
        }
    }
}
