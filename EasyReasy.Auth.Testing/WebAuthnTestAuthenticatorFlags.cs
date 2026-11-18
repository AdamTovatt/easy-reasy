namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// The settable flags a <see cref="WebAuthnTestAuthenticator"/> reports in its authenticator data, read
    /// once per ceremony so the bytes it signs are the ones it was set to at that moment.
    /// </summary>
    internal readonly struct WebAuthnTestAuthenticatorFlags
    {
        /// <summary>Whether to report the user-verified flag.</summary>
        public bool VerifiesUser { get; }

        /// <summary>Whether to report the backup-eligible flag.</summary>
        public bool BackupEligible { get; }

        /// <summary>Whether to report the backed-up flag.</summary>
        public bool BackedUp { get; }

        /// <summary>
        /// Captures the flags to report.
        /// </summary>
        /// <param name="verifiesUser">Whether to report the user-verified flag.</param>
        /// <param name="backupEligible">Whether to report the backup-eligible flag.</param>
        /// <param name="backedUp">Whether to report the backed-up flag.</param>
        public WebAuthnTestAuthenticatorFlags(bool verifiesUser, bool backupEligible, bool backedUp)
        {
            VerifiesUser = verifiesUser;
            BackupEligible = backupEligible;
            BackedUp = backedUp;
        }
    }
}
