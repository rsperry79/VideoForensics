using Microsoft.AspNetCore.Components;

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
    }
}
