namespace Domino.UI.AppShell
{
    // Presentation copy follows the existing EN/ES presentation helpers; no domain keys.
    public static class AccountProtectionText
    {
        public static string Title(string locale)=>locale=="es"?"Protege tu cuenta":"Protect your account";
        public static string Warning(string locale)=>locale=="es"
            ?"Esta cuenta de invitado no está vinculada a un método de inicio de sesión. Si cierras sesión, es posible que no puedas recuperar este perfil y su progreso."
            :"This guest account is not linked to a sign-in method. If you log out, you may not be able to recover this profile and its progress.";
        public static string Protect(string locale)=>locale=="es"?"Proteger cuenta":"Protect account";
        public static string Anyway(string locale)=>locale=="es"?"Cerrar sesión de todos modos":"Log out anyway";
        public static string Cancel(string locale)=>locale=="es"?"Cancelar":"Cancel";
        public static string Entry(string locale)=>locale=="es"?"La protección por correo electrónico estará disponible próximamente. Tu sesión sigue activa.":"Email account protection is coming soon. Your session is still active.";
        public static string Unavailable(string locale)=>locale=="es"?"No se pudo comprobar el método de acceso. Tu sesión no se ha cerrado.":"Your sign-in method could not be checked. You have not been signed out.";
        public static string SigningOut(string locale)=>locale=="es"?"Cerrando sesión…":"Signing out…";
        public static string LogoutError(string locale)=>locale=="es"?"No se pudo cerrar sesión de forma segura. Cierra cualquier partida o cola activa y vuelve a intentarlo.":"Sign out could not finish safely. Close any active game or queue, then try again.";
        public static string Retry(string locale)=>locale=="es"?"Reintentar":"Try again";
    }
}
