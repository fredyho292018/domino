namespace Domino.Player
{
    public static class DisplayNameRules
    {
        public static bool IsValid(string value)
        {
            if (value == null || value.Length < 3 || value.Length > 16) return false;
            foreach (char c in value)
                if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '-')) return false;
            return true;
        }
        public static bool IsGenerated(string value) => value != null &&
            System.Text.RegularExpressions.Regex.IsMatch(value, "\\AGuest-[A-Z0-9]{8}\\z");
    }
}
