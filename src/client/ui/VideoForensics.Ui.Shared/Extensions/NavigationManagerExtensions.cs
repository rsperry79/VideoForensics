using Microsoft.AspNetCore.Components;
using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.Ui.Shared.Extensions
{
    public static class NavigationManagerExtensions
    {
        /// <summary>
        /// The "/signin" route for the current page, carrying a returnUrl back to it - every
        /// "You are not signed in" prompt across the app links here instead of a bare "/signin" so
        /// signing in doesn't strand the user back on the dashboard. SignIn.razor's GetReturnUrl()
        /// only trusts a relative path parsed back out of this, never an absolute/external URL.
        /// </summary>
        public static string SignInPathWithReturnUrl(this NavigationManager navigationManager)
        {
            string returnUrl = "/" + navigationManager.ToBaseRelativePath(navigationManager.Uri);
            return $"/signin?returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        /// <summary>
        /// The "/signin" route with a context parameter that indicates whether this is a fresh sign-in
        /// ("signin") or a re-authentication challenge ("challenge"). Stores the return URL and context
        /// in the session state for use by the sign-in page.
        /// </summary>
        public static string SignInPathWithContext(this NavigationManager navigationManager,
            PairedSessionState session, bool wasSignedIn = false)
        {
            string returnUrl = "/" + navigationManager.ToBaseRelativePath(navigationManager.Uri);
            string context = wasSignedIn ? "challenge" : "signin";
            session.AuthReturnUrl = returnUrl;
            session.AuthContext = context;
            return $"/signin?returnUrl={Uri.EscapeDataString(returnUrl)}&context={context}";
        }
    }
}
