using System;
using Domino.Identity;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.UI.AppShell
{
    public sealed class ProductionProtectAccountView : ProductionRootPage
    {
        readonly AccountLinkController controller;readonly string locale;
        readonly ProductionPasswordField password,confirmation;
        readonly Label emailError,passwordError,confirmationError;
        readonly AuthStatusMessage feedback;
        readonly ThemeButton submit,cancel;
        VisualElement focused;float keyboardInset;
        public TextField Email {get;}
        public TextField Password=>password.Field;
        public TextField Confirmation=>confirmation.Field;
        public ProductionProtectAccountView(AccountLinkController controller,Action cancelled,string locale):base("","")
        {
            this.controller=controller;this.locale=locale;name="ProtectAccountForm";Body.Clear();
            style.backgroundColor=ThemeProvider.Current.Colors.Background;
            verticalScrollerVisibility=horizontalScrollerVisibility=ScrollerVisibility.Hidden;
            Label Copy(string text,TextRole role){var label=new Label(text);ThemeStyles.Text(label,role);label.style.whiteSpace=WhiteSpace.Normal;label.style.flexShrink=0;label.style.marginBottom=12;Body.Add(label);return label;}
            Copy(AccountProtectionText.Protect(locale),TextRole.PageTitle).name="ProtectTitle";
            Copy(T("Add an email and password so you can sign in again and keep this profile.","Añade un correo y una contraseña para volver a iniciar sesión y conservar este perfil."),TextRole.Secondary);
            var emailLabel=Copy(T("Email","Correo electrónico"),TextRole.Body);emailLabel.style.marginBottom=8;
            Email=new TextField{name="LinkEmail",tooltip=emailLabel.text,keyboardType=TouchScreenKeyboardType.EmailAddress};ThemeStyles.Text(Email,TextRole.Body);
            Email.style.height=Email.style.minHeight=48;Email.style.flexShrink=0;Email.style.marginBottom=8;
            var input=Email.Q(className:"unity-text-field__input");if(input!=null){input.style.backgroundColor=ThemeProvider.Current.Colors.Surface;input.style.color=ThemeProvider.Current.Colors.TextPrimary;ThemeStyles.Pad(input,12);ThemeStyles.Round(input,12);}
            Body.Add(Email);emailError=Copy("",TextRole.Secondary);emailError.name="LinkEmailError";
            Copy(T("Password","Contraseña"),TextRole.Body).style.marginBottom=8;
            password=new ProductionPasswordField("LinkPassword",T("password","contraseña"));Body.Add(password);passwordError=Copy("",TextRole.Secondary);passwordError.name="LinkPasswordError";
            Copy(T("Confirm password","Confirmar contraseña"),TextRole.Body).style.marginBottom=8;
            confirmation=new ProductionPasswordField("LinkConfirmation",T("confirmation","confirmación"));Body.Add(confirmation);confirmationError=Copy("",TextRole.Secondary);confirmationError.name="LinkConfirmationError";
            Password.tooltip=T("Password","Contraseña");Confirmation.tooltip=T("Confirm password","Confirmar contraseña");
            foreach(var error in new[]{emailError,passwordError,confirmationError}){error.style.color=ThemeProvider.Current.Colors.Error;error.style.display=DisplayStyle.None;}
            feedback=new AuthStatusMessage();Body.Add(feedback);
            submit=new ThemeButton(AccountProtectionText.Protect(locale),()=>{_=controller.SubmitAsync(Email.value,Password.value,Confirmation.value);},true){name="LinkSubmit"};submit.style.whiteSpace=WhiteSpace.Normal;Body.Add(submit);
            cancel=new ThemeButton(AccountProtectionText.Cancel(locale),()=>{if(controller.CanCancel)cancelled();}){name="LinkCancel"};Body.Add(cancel);
            foreach(var field in new[]{Email,Password,Confirmation}){
                field.RegisterValueChangedCallback(_=>Present());
                field.RegisterCallback<FocusInEvent>(_=>{focused=field;BringIntoView(field);});
            }
            RegisterCallback<GeometryChangedEvent>(_=>{if(focused!=null)BringIntoView(focused);});
            RegisterCallback<DetachFromPanelEvent>(_=>{controller.Changed-=Present;Password.SetValueWithoutNotify("");Confirmation.SetValueWithoutNotify("");});
            controller.Changed+=Present;
            schedule.Execute(()=>{if(panel==null)return;float inset=TouchScreenKeyboard.visible&&Screen.height>0?TouchScreenKeyboard.area.height*panel.visualTree.layout.height/Screen.height:0;SetKeyboardInset(inset);}).Every(100);
            Present();
        }
        string T(string en,string es)=>locale=="es"?es:en;
        public void SetKeyboardInset(float inset){if(Mathf.Abs(keyboardInset-inset)<1)return;keyboardInset=Mathf.Max(0,inset);style.marginBottom=keyboardInset;if(focused!=null)BringIntoView(focused);}
        public void BringIntoView(VisualElement field){schedule.Execute(()=>{if(field.panel==panel)ScrollTo(field);});}
        string ErrorCopy(EmailAuthError error){switch(error){
            case EmailAuthError.InvalidEmail:return T("Enter a valid email address.","Introduce un correo electrónico válido.");
            case EmailAuthError.WeakPassword:return T("Use a password with 6 to 4096 characters.","Usa una contraseña de 6 a 4096 caracteres.");
            case EmailAuthError.PasswordMismatch:return T("Passwords do not match.","Las contraseñas no coinciden.");
            case EmailAuthError.EmailAlreadyInUse:return T("This email is already linked to another account.","Este correo ya está vinculado a otra cuenta.");
            case EmailAuthError.NetworkError:return T("Could not connect. Your profile is still here. Try again.","No se pudo conectar. Tu perfil sigue aquí. Inténtalo de nuevo.");
            default:return T("This action could not be completed safely. Try again.","No se pudo completar esta acción de forma segura. Inténtalo de nuevo.");
        }}
        void Present(){
            var error=controller.Error;
            void FieldError(Label label,bool show){label.text=show?ErrorCopy(error):"";label.tooltip=label.text;label.style.display=show?DisplayStyle.Flex:DisplayStyle.None;}
            FieldError(emailError,error==EmailAuthError.InvalidEmail||error==EmailAuthError.EmailAlreadyInUse);
            FieldError(passwordError,error==EmailAuthError.WeakPassword);FieldError(confirmationError,error==EmailAuthError.PasswordMismatch);
            password.SetError(error==EmailAuthError.WeakPassword);confirmation.SetError(error==EmailAuthError.PasswordMismatch);
            var local=EmailAuthRules.Validate(Email.value,Password.value,Confirmation.value);
            submit.SetEnabled(!controller.Busy&&(controller.State==AccountLinkState.ErrorAfterLink?controller.CanRetryAfterLink:local==EmailAuthError.None&&controller.CanCancel));
            submit.text=controller.State==AccountLinkState.ErrorAfterLink?AccountProtectionText.Retry(locale):AccountProtectionText.Protect(locale);
            cancel.SetEnabled(controller.CanCancel);cancel.style.display=controller.CanCancel?DisplayStyle.Flex:DisplayStyle.None;
            bool editable=!controller.Busy&&controller.CanCancel;
            Email.SetEnabled(editable);password.SetEnabled(editable);confirmation.SetEnabled(editable);
            if(controller.Busy)feedback.PresentSemantic(AuthStatusVariant.Loading,T("Protecting your account…","Protegiendo tu cuenta…"));
            else if(controller.State==AccountLinkState.ErrorAfterLink)feedback.PresentSemantic(AuthStatusVariant.Warning,T("Your sign-in method was linked. Continue verification; the link will not be repeated.","Tu método de acceso está vinculado. Continúa con la verificación; no se repetirá la vinculación."));
            else if(error!=EmailAuthError.None&&error!=EmailAuthError.InvalidEmail&&error!=EmailAuthError.EmailAlreadyInUse&&error!=EmailAuthError.WeakPassword&&error!=EmailAuthError.PasswordMismatch)feedback.PresentSemantic(AuthStatusVariant.Error,ErrorCopy(error));
            else feedback.Hide();
            if(controller.State==AccountLinkState.LinkedUnverified||controller.State==AccountLinkState.VerificationSending||controller.State==AccountLinkState.VerificationPending){Password.SetValueWithoutNotify("");Confirmation.SetValueWithoutNotify("");}
        }
    }
}
