using UnityEngine;

namespace Project.Core.Settings
{
    // Team contact and legal pages; an empty field hides its row or link.
    [CreateAssetMenu(menuName = "Project/App Links", fileName = "AppLinks")]
    public sealed class AppLinks : ScriptableObject
    {
        [SerializeField] private string _supportEmail = "";
        [SerializeField] private string _privacyUrl = "";
        [SerializeField] private string _termsUrl = "";

        public string SupportEmail => Clean(_supportEmail);
        public string PrivacyUrl => Clean(_privacyUrl);
        public string TermsUrl => Clean(_termsUrl);

        public void Set(string supportEmail, string privacyUrl, string termsUrl)
        {
            _supportEmail = supportEmail;
            _privacyUrl = privacyUrl;
            _termsUrl = termsUrl;
        }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
    }
}
