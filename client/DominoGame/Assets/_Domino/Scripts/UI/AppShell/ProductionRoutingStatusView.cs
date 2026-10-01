using System;
using Domino.Identity;
using Domino.UI.Theming;
using UnityEngine.UIElements;

namespace Domino.UI.AppShell
{
    public sealed class ProductionRoutingStatusView : ScrollView
    {
        public ProductionRoutingStatusView(ProductionAuthRoute route,string message,Action retry):base(ScrollViewMode.Vertical)
        {
            name="ProductionRoutingStatus";style.flexGrow=1;style.minHeight=0;style.backgroundColor=ThemeProvider.Current.Colors.Background;
            verticalScrollerVisibility=horizontalScrollerVisibility=ScrollerVisibility.Hidden;
            var body=new VisualElement();ThemeStyles.Page(body);body.style.flexShrink=0;Add(body);
            bool loading=route==ProductionAuthRoute.Loading,update=route==ProductionAuthRoute.UpdateRequired;
            var title=new Label(loading?"Loading your session":update?"Update required":"Could not connect");ThemeStyles.Text(title,TextRole.PageTitle);title.style.whiteSpace=WhiteSpace.Normal;body.Add(title);
            var feedback=new AuthStatusMessage();body.Add(feedback);feedback.PresentSemantic(loading?AuthStatusVariant.Loading:update?AuthStatusVariant.Warning:AuthStatusVariant.Error,loading?"Please wait…":message);
            if(!loading&&!update)body.Add(new ThemeButton("Retry",retry,true){name="RoutingRetry"});
        }
    }
}
