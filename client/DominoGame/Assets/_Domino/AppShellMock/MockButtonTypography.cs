using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.AppShellMock
{
    public static class MockButtonTypography
    {
        static Font bold,semibold,medium;
        public static int Weight(string label,bool primary)
        {
            if(primary)return 700;
            switch(label)
            {
                case "‹  Back": case "Not Now": case "Skip": case "Cancel":
                case "Forgot Password": case "Resend": case "Resend Code":
                    return 500;
                default:return 600;
            }
        }
        public static void Apply(Button button,bool primary)
        {
            int weight=Weight(button.text,primary);
            Font font;
            if(weight==700)font=bold?bold: bold=Load("Bold");
            else if(weight==600)font=semibold?semibold: semibold=Load("Semibold");
            else font=medium?medium: medium=Load("Medium");
            button.style.unityFont=font;
            button.style.unityFontDefinition=FontDefinition.FromFont(font);
            // Weight is supplied by the font face, not synthetic bolding.
            button.style.unityFontStyleAndWeight=FontStyle.Normal;
            button.AddToClassList("mock-button-weight-"+weight);
        }
        static Font Load(string face)
        {
            var font=Resources.Load<Font>("AppShellMockFonts/SourceSans3-"+face);
            if(!font)throw new InvalidOperationException("Missing mock button font: "+face);
            return font;
        }
    }
}
