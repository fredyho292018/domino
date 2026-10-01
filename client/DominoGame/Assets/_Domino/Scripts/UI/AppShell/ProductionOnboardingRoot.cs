using System;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.UI.AppShell
{
    // One root, no auth/session acquisition and no application routing side effects.
    public sealed class ProductionOnboardingRoot : ScrollView, IDisposable
    {
        readonly OnboardingShellController controller;
        public VisualElement Body {get;}=new VisualElement{name="OnboardingBody"};
        public ProductionOnboardingRoot(OnboardingShellController controller):base(ScrollViewMode.Vertical)
        {
            this.controller=controller;name="ProductionOnboardingRoot";style.flexGrow=1;style.minHeight=0;
            style.backgroundColor=ThemeProvider.Current.Colors.Background;
            verticalScrollerVisibility=horizontalScrollerVisibility=ScrollerVisibility.Hidden;
            contentContainer.style.flexGrow=1;ThemeStyles.Page(Body);Body.style.flexShrink=0;Add(Body);
            controller.Changed+=Render;Render();
        }
        void Text(string value,TextRole role,string key){var label=new Label(value){name=key};ThemeStyles.Text(label,role);label.style.whiteSpace=WhiteSpace.Normal;Body.Add(label);}
        string coachLocale,coachFingerprint;
        void Render()
        {
            var fingerprint=Newtonsoft.Json.JsonConvert.SerializeObject(controller.Coaches);var existing=Body.Q<ProductionCoachView>();if(existing!=null&&controller.Phase==OnboardingShellPhase.InProgress&&controller.State.currentStepKey=="COACH_STEP"&&coachLocale==controller.Locale&&coachFingerprint==fingerprint){existing.Refresh();var oldBack=Body.Q<Button>("CoachBack");if(oldBack!=null)oldBack.SetEnabled(controller.CoachCanBack);return;}
            var hadMembership=Body.Q<ProductionMembershipView>()!=null;var focusedName=(panel?.focusController?.focusedElement as VisualElement)?.name;var scrollBefore=scrollOffset;Body.Clear();bool es=controller.Locale=="es";
            var phase=controller.Phase;var step=controller.CurrentStep;
            if(phase==OnboardingShellPhase.InProgress&&step.key=="MEMBERSHIP_STEP"){var back=new Button(()=>{_ =controller.BackMembershipAsync();}){name="MembershipBack"};var arrow=new Image();ThemeStyles.Back(back,arrow);back.Add(arrow);back.style.width=back.style.height=44;back.style.alignSelf=Align.FlexStart;back.SetEnabled(controller.CanBackMembership);Body.Add(back);}
            if(phase==OnboardingShellPhase.InProgress&&step.key=="CONTACTS_STEP"){var back=new Button(()=>{_ =controller.BackContactsAsync();}){name="ContactsBack"};var arrow=new Image();ThemeStyles.Back(back,arrow);back.Add(arrow);back.style.width=back.style.height=44;back.style.alignSelf=Align.FlexStart;back.SetEnabled(controller.ContactsCanBack);Body.Add(back);}
            if(phase==OnboardingShellPhase.InProgress&&step.key=="COACH_STEP"){var back=new Button(()=>{_ =controller.BackCoachAsync();}){name="CoachBack"};var arrow=new Image();ThemeStyles.Back(back,arrow);back.Add(arrow);back.style.width=back.style.height=44;back.style.alignSelf=Align.FlexStart;back.SetEnabled(controller.CoachCanBack);Body.Add(back);}
            if(phase==OnboardingShellPhase.InProgress&&step.key=="EXPERIENCE_STEP"&&controller.State.catalogVersion==2){var back=new Button(()=>{_ =controller.BackExperienceAsync();}){name="ExperienceBack"};var arrow=new Image();ThemeStyles.Back(back,arrow);back.Add(arrow);back.style.width=back.style.height=44;back.style.alignSelf=Align.FlexStart;back.SetEnabled(controller.ExperienceCanBack);Body.Add(back);}
            Text(phase==OnboardingShellPhase.Completed?(es?"TODO LISTO":"YOU'RE READY"):phase==OnboardingShellPhase.InProgress&&step.key=="MEMBERSHIP_STEP"?(es?"TU MEMBRESÍA":"YOUR MEMBERSHIP"):phase==OnboardingShellPhase.InProgress&&step.key=="CONTACTS_STEP"?(es?"TU GENTE":"YOUR PEOPLE"):phase==OnboardingShellPhase.InProgress&&step.key=="COACH_STEP"?(es?"TU ENTRENADOR":"YOUR COACH"):phase==OnboardingShellPhase.InProgress&&step.key=="EXPERIENCE_STEP"?step.title:phase==OnboardingShellPhase.InProgress&&step.key=="BASIC_PROFILE_STEP"?BasicProfileCopy.Eyebrow(controller.Locale):es?"TU CLUB":"YOUR CLUB",TextRole.Secondary,"OnboardingEyebrow");
            Text(phase==OnboardingShellPhase.InProgress?(step.key=="BASIC_PROFILE_STEP"?BasicProfileCopy.Title(controller.Locale):step.key=="EXPERIENCE_STEP"?controller.ExperienceQuestion?.title??step.title:step.title):phase==OnboardingShellPhase.Completed?(es?"Todo listo":"All set"):(es?"Bienvenido":"Welcome"),TextRole.PageTitle,"OnboardingTitle");
            if(phase==OnboardingShellPhase.InProgress&&step.key=="EXPERIENCE_STEP")Body.Q<Label>("OnboardingEyebrow").style.color=ThemeProvider.Current.Colors.TextSecondaryEmphasized;
            var feedback=new AuthStatusMessage();Body.Add(feedback);
            if(phase==OnboardingShellPhase.Loading)feedback.PresentSemantic(AuthStatusVariant.Loading,es?"Cargando perfil…":"Loading profile…");
            else if(phase==OnboardingShellPhase.Error)feedback.PresentSemantic(AuthStatusVariant.Error,es?"No se pudo cargar. Inténtalo de nuevo.":"Could not load. Try again.");
            else if(phase==OnboardingShellPhase.UpdateRequired)feedback.PresentSemantic(AuthStatusVariant.Warning,es?"Actualiza la aplicación para continuar.":"Update the app to continue.");
            else if(phase==OnboardingShellPhase.Completed)feedback.PresentSemantic(AuthStatusVariant.Success,es?"Tu perfil está listo para jugar.":"Your profile is ready to play.");
            else if(phase==OnboardingShellPhase.InProgress){
                Text(step.description??"",TextRole.Secondary,"OnboardingDescription");
                if(step.key=="BASIC_PROFILE_STEP"){Body.Add(new ProductionBasicProfileView(controller));return;}
                if(step.key=="EXPERIENCE_STEP"){Body.Add(new ProductionExperienceView(controller));return;}
                if(step.key=="COACH_STEP"){coachLocale=controller.Locale;coachFingerprint=fingerprint;Body.Add(new ProductionCoachView(controller));if(existing!=null)schedule.Execute(()=>scrollOffset=scrollBefore);return;}
                if(step.key=="MEMBERSHIP_STEP"){Body.Q<Label>("OnboardingEyebrow").style.display=DisplayStyle.None;Body.Q<Label>("OnboardingDescription").style.display=DisplayStyle.None;var heading=Body.Q<Label>("OnboardingTitle");heading.style.unityTextAlign=TextAnchor.MiddleCenter;heading.style.fontSize=27;heading.style.marginBottom=22;Body.Add(new ProductionMembershipView(controller));if(hadMembership)schedule.Execute(()=>{scrollOffset=scrollBefore;if(!string.IsNullOrEmpty(focusedName)){var focus=Body.Q<Button>(focusedName);if(focus?.enabledInHierarchy==true)focus.Focus();}});return;}
                if(step.key=="CONTACTS_STEP"){Body.Add(new ProductionContactsView(controller));return;}
                var placeholder=new VisualElement{name=step.key};ThemeStyles.Card(placeholder);Body.Add(placeholder);
                var label=new Label(es?"Pantalla pendiente de integración.":"Screen integration pending.");ThemeStyles.Text(label,TextRole.Body);label.style.whiteSpace=WhiteSpace.Normal;placeholder.Add(label);
            }else if(phase==OnboardingShellPhase.NotStarted){
                Text(es?"Tu configuración aún no ha comenzado.":"Your setup has not started yet.",TextRole.Body,"OnboardingNotStarted");
                if(controller.CanStart||controller.Busy){var start=new ThemeButton(controller.FlowRetry?(es?"Reintentar":"Retry"):(es?"Comenzar":"Get started"),()=>{_ =controller.StartAsync();},true){name="OnboardingStart"};start.SetEnabled(controller.CanStart);Body.Add(start);}
                if(!string.IsNullOrEmpty(controller.FlowFeedback))feedback.PresentSemantic(controller.Busy?AuthStatusVariant.Loading:AuthStatusVariant.Error,controller.Busy?(es?"Guardando progreso…":"Saving progress…"):(es?"No se pudo iniciar. Inténtalo de nuevo.":"Could not start. Try again."));
            }
            if(controller.CanRetry)Body.Add(new ThemeButton(es?"Reintentar":"Retry",()=>{_ =controller.RetryAsync();},true){name="OnboardingRetry"});
        }
        public void SetSafeArea(float top,float bottom){Body.style.paddingTop=24+Mathf.Max(0,top);Body.style.paddingBottom=24+Mathf.Max(0,bottom);}
        public void Dispose(){controller.Changed-=Render;}
    }
}
