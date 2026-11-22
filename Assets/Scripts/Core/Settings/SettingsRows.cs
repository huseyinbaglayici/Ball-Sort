using System;

namespace Project.Core.Settings
{
    // Which optional settings rows and footer links show.
    public readonly struct SettingsRows
    {
        public readonly bool Language;
        public readonly bool Feedback;
        public readonly bool Privacy;
        public readonly bool Terms;

        public SettingsRows(bool language, bool feedback, bool privacy, bool terms)
        {
            Language = language;
            Feedback = feedback;
            Privacy = privacy;
            Terms = terms;
        }

        // How to play always shows; language needs a second table to pick.
        public static SettingsRows For(int languageCount, AppLinks links) => new(
            languageCount > 1,
            links != null && links.SupportEmail.Length > 0,
            links != null && links.PrivacyUrl.Length > 0,
            links != null && links.TermsUrl.Length > 0);

        // Language, help, feedback, plus the copy-address row once mail could not open.
        public int Count(bool copyAddress) => (Language ? 1 : 0) + 1 + (Feedback ? 1 : 0) + (Feedback && copyAddress ? 1 : 0);

        public static string FeedbackMail(string address, string subject, string body) =>
            $"mailto:{address}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
    }
}
