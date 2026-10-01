using System;
using System.Collections.Generic;
using UnityEngine;

namespace Domino.UI.Theming
{
    public enum ThemeId { MODERN_SOCIAL_PREMIUM }
    public enum TextRole { PageTitle, SectionTitle, Body, Secondary, Caption, ButtonPrimary, ButtonSecondary, NavigationLabel }
    public enum ComponentState { Normal, Hover, Pressed, Selected, Disabled, Focused }
    public sealed class ThemeColors
    {
        public readonly Color Primary=UiKit.Hex("71A84B"), Background=UiKit.Hex("2A2623"), Surface=UiKit.Hex("41403C");
        public readonly Color TextPrimary=UiKit.Hex("FBFAFA"), TextSecondary=UiKit.Hex("969495"), IconActive=UiKit.Hex("FBFAFA"), IconInactive=UiKit.Hex("969495");
        public readonly Color Success=UiKit.Hex("71A84B"), Error=UiKit.Hex("E89898"), Warning=UiKit.Hex("E5BE67"), Info=UiKit.Hex("69B9E8");
        public Color TextSecondaryEmphasized=>Color.Lerp(TextSecondary,TextPrimary,.2f);
        public Color Selected=>Primary; public Color Disabled=>TextSecondary;
        public readonly Color Win=UiKit.Hex("B8DEA5"), Loss=UiKit.Hex("E8B0B0");
        public Color DiamondAccent=>Info; public readonly Color PlatinumAccent=UiKit.Hex("C9CDD5");
        public Color GoldAccent=>Warning; public Color FamilyAccent=>Primary;
    }
    public readonly struct TypographyToken
    {
        public readonly int Size,Weight;
        public TypographyToken(int size,int weight){Size=size;Weight=weight;}
    }
    public sealed class ThemeTypography
    {
        public TypographyToken this[TextRole role]
        {
            get { switch(role) {
                case TextRole.PageTitle:return new TypographyToken(29,700);
                case TextRole.SectionTitle:return new TypographyToken(20,700);
                case TextRole.Body:return new TypographyToken(15,500);
                case TextRole.Secondary:return new TypographyToken(13,500);
                case TextRole.Caption:return new TypographyToken(11,500);
                case TextRole.ButtonPrimary:return new TypographyToken(15,700);
                case TextRole.ButtonSecondary:return new TypographyToken(15,600);
                case TextRole.NavigationLabel:return new TypographyToken(11,600);
                default:throw new ArgumentOutOfRangeException(nameof(role));
            }}
        }
        public Font FontFor(TextRole role)=>FontForWeight(this[role].Weight);
        static readonly Dictionary<int,Font> Fonts=new Dictionary<int,Font>();
        static Font FontForWeight(int weight)
        {
            if(Fonts.TryGetValue(weight,out var font)&&font!=null)return font;
            string face=weight==700?"Bold":weight==600?"Semibold":"Medium";
            font=Resources.Load<Font>("AppShellMockFonts/SourceSans3-"+face);
            if(font==null)throw new InvalidOperationException("Bundled theme font missing: "+face);
            Fonts[weight]=font;return font;
        }
    }
    public sealed class ThemeSpacing
    {
        public readonly float XS=4,SM=8,MD=12,LG=20,XL=24;
        public float PageMargin=>XL; public float CardPadding=>LG; public float RowGap=>SM;
        public float SectionGap=>XL; public float IconTextGap=>MD;
    }
    public sealed class ThemeRadius
    {
        public readonly float Card=16,Button=12,Input=10,Segmented=12,IconContainer=17;
    }
    public sealed class ThemeSizing
    {
        public readonly float MinTouchTarget=44,ButtonHeight=48,MenuRowHeight=56,ProfileTouchHeight=50,BottomTabTouchHeight=60,NavIconLogicalSize=26,AuthRowHeight=56,AuthIconContainer=34,ContentMaxWidth=620;
    }
    public sealed class AppTheme
    {
        internal AppTheme(){}
        public ThemeId Id=>ThemeId.MODERN_SOCIAL_PREMIUM;
        public string DisplayName=>"ModernSocialPremium";
        public ThemeColors Colors {get;}=new ThemeColors();
        public ThemeTypography Typography {get;}=new ThemeTypography();
        public ThemeSpacing Spacing {get;}=new ThemeSpacing();
        public ThemeRadius Radius {get;}=new ThemeRadius();
        public ThemeSizing Sizing {get;}=new ThemeSizing();
        public string BackIconResource=>"AppShellMockIcons/icon_arrow_left";
        public IReadOnlyList<string> BottomTabs {get;}=Array.AsReadOnly(new[]{"HOME","PUZZLES","LEARN","WATCH","MENU"});
    }
    public static class ThemeProvider
    {
        public static AppTheme Current {get;}=new AppTheme();
        public static AppTheme Resolve(ThemeId? preference=null)
        {
            if(preference.HasValue&&preference.Value!=Current.Id)throw new ArgumentOutOfRangeException(nameof(preference));
            return Current;
        }
    }
}
