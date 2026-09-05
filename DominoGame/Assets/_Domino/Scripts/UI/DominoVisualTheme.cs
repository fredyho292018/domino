using UnityEngine;

namespace Domino.UI
{
    // Shared presentation palette. TileStyles remains the saved tile-skin selector.
    public static class DominoVisualTheme
    {
        public const string Name = "ModernSocialPremium";
        public static readonly Color Background = UiKit.Hex("080F19");
        public static readonly Color BackgroundLight = UiKit.Hex("192D35");
        public static readonly Color Table = UiKit.Hex("153F39");
        public static readonly Color TableLight = UiKit.Hex("296356");
        public static readonly Color Rail = UiKit.Hex("17312F");
        public static readonly Color Accent = UiKit.Hex("CDB17C");
        public static readonly Color Text = UiKit.Hex("F3EFE5");
        public static readonly Color SecondaryText = UiKit.Hex("A8B9B7");
        public static readonly Color TileFace = UiKit.Hex("F5F0E3");
        public static readonly Color Pip = UiKit.Hex("101719");
        public static readonly Color TeamA = UiKit.Hex("69988C");
        public static readonly Color TeamB = UiKit.Hex("8293B0");
        public const float ContactShadowAlpha = .28f;
        public const float SelectedShadowAlpha = .38f;
    }
}
