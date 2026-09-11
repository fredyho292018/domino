using System;
using UnityEngine;

namespace Domino.UI
{
    public enum TileStyle { TeamFhoIvory, CubaBlue, TeamFhoWhite }
    public static class TileStyles
    {
        const string PreferenceKey = "Domino.TileStyle";
        static TileStyle? current;
        static Sprite brandLogo;
        static Sprite brandMark;
        public static Sprite BrandMark
        {
            get
            {
                if (brandMark) return brandMark;
                var texture = Resources.Load<Texture2D>("Domino/TeamFhoBrand");
                if (!texture) return null;
                return brandMark = Sprite.Create(texture,new Rect(18,9,47,47),Vector2.one*.5f);
            }
        }
        public static Sprite BrandLogo
        {
            get
            {
                if (brandLogo) return brandLogo;
                var texture = Resources.Load<Texture2D>("Domino/TeamFhoBrand");
                if (!texture) return null;
                brandLogo = Sprite.Create(texture, new Rect(0,0,texture.width,texture.height), Vector2.one*.5f);
                return brandLogo;
            }
        }
        public static event Action Changed;
        public static TileStyle Current
        {
            get
            {
                if (!current.HasValue)
                {
                    int saved = PlayerPrefs.GetInt(PreferenceKey, 0);
                    current = saved >= 0 && saved <= 2 ? (TileStyle)saved : TileStyle.TeamFhoIvory;
                }
                return current.Value;
            }
        }
        public static void Set(TileStyle style)
        {
            if (Current == style) return;
            current = style;
            PlayerPrefs.SetInt(PreferenceKey, (int)style); PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
