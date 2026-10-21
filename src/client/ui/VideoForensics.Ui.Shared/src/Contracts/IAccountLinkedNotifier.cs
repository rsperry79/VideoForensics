namespace VideoForensics.Ui.Shared.Contracts
{
    /// <summary>Publishing side of the account-linked stream, used by the add-account wizard after a link succeeds.</summary>
    public interface IAccountLinkedNotifier
    {
        /// <summary>Announces that the ProviderAccount with the given id was linked.</summary>
        /// <param name="providerAccountId">Id of the newly linked ProviderAccount.</param>
        void Publish(Guid providerAccountId);
    }
}
