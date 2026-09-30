using System;
using System.Linq;
using System.Net.Mail;
namespace Domino.Identity
{
    public enum EmailAuthError { None, InvalidEmail, WeakPassword, PasswordMismatch, EmailAlreadyInUse, TooManyRequests, NetworkError, GuestUpgradeRequired, SessionConflict, Unknown, PasswordRequired, InvalidCredential, UserDisabled }
    public enum EmailOperationState { Idle, Submitting, Routing, Checking, Resending, Error, SigningIn, Resetting }
    public enum AuthSessionKind { NoSession, RestoredGuest, EmailUnverified, EmailVerified, OtherRegistered }
    public sealed class EmailAuthException : Exception
    {
        public EmailAuthError Code { get; }
        public EmailAuthException(EmailAuthError code):base(EmailAuthRules.Message(code)){Code=code;}
    }
    public sealed class FirebaseAuthSessionSnapshot
    {
        public string Uid { get; }
        public bool IsAnonymous { get; }
        public bool IsEmailVerified { get; }
        public bool IsPasswordProvider { get; }
        public string DisplayEmail { get; }
        public FirebaseAuthSessionSnapshot(string uid,bool anonymous,bool verified,bool passwordProvider,string displayEmail="")
        {Uid=uid;IsAnonymous=anonymous;IsEmailVerified=verified;IsPasswordProvider=passwordProvider;DisplayEmail=displayEmail??"";}
    }
    public static class EmailAuthRules
    {
        public static string Normalize(string email)=>(email??"").Trim();
        public const string ResetSuccess="If an account exists for this email, password reset instructions have been sent.";
        public static EmailAuthError ValidateEmail(string email)
        {
            email=Normalize(email);
            try{return string.IsNullOrEmpty(email)||email.Any(char.IsWhiteSpace)||new MailAddress(email).Address!=email||!email.Contains("@")?EmailAuthError.InvalidEmail:EmailAuthError.None;}
            catch{return EmailAuthError.InvalidEmail;}
        }
        public static EmailAuthError ValidateSignIn(string email,string password)
        {var error=ValidateEmail(email);return error!=EmailAuthError.None?error:string.IsNullOrEmpty(password)?EmailAuthError.PasswordRequired:EmailAuthError.None;}
        public static EmailAuthError Validate(string email,string password,string confirmation)
        {
            email=Normalize(email);
            try {if(string.IsNullOrEmpty(email)||email.Any(char.IsWhiteSpace)||new MailAddress(email).Address!=email||!email.Contains("@"))return EmailAuthError.InvalidEmail;}
            catch{return EmailAuthError.InvalidEmail;}
            if(password==null||password.Length<6||password.Length>4096)return EmailAuthError.WeakPassword;
            return password!=confirmation?EmailAuthError.PasswordMismatch:EmailAuthError.None;
        }
        public static EmailAuthError FirebaseCode(int code)=>code==11?EmailAuthError.InvalidEmail:code==23?EmailAuthError.WeakPassword:code==8?EmailAuthError.EmailAlreadyInUse:code==13||code==39?EmailAuthError.TooManyRequests:code==19?EmailAuthError.NetworkError:EmailAuthError.Unknown;
        public static string Message(EmailAuthError code){switch(code){
            case EmailAuthError.InvalidEmail:return "Enter a valid email address.";
            case EmailAuthError.PasswordRequired:return "Enter your password.";
            case EmailAuthError.InvalidCredential:return "Check your email and password and try again.";
            case EmailAuthError.UserDisabled:return "Unable to sign in. Please contact support.";
            case EmailAuthError.WeakPassword:return "Use a password with 6 to 4096 characters.";
            case EmailAuthError.PasswordMismatch:return "Passwords do not match.";
            case EmailAuthError.EmailAlreadyInUse:return "Email already registered.";
            case EmailAuthError.TooManyRequests:return "Too many requests. Please wait before trying again.";
            case EmailAuthError.NetworkError:return "Check your connection and try again.";
            case EmailAuthError.GuestUpgradeRequired:return "This Guest account must be secured using the account upgrade flow.";
            case EmailAuthError.SessionConflict:return "The current session cannot be replaced here.";
            default:return "We could not complete this action. Please try again.";
        }}
    }
}
