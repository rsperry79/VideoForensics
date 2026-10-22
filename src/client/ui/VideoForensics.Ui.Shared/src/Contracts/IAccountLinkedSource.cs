namespace VideoForensics.Ui.Shared.Contracts
{
    /// <summary>
    /// Host-agnostic source of "an account was just linked" events, so an open accounts list can refresh without a
    /// reload. Events are not replayed: a new subscriber hears only later events. Subscribers must dispose their subscription.
    /// </summary>
    public interface IAccountLinkedSource
    {
        /// <summary>Emits the ProviderAccount id each time an account is linked.</summary>
        IObservable<Guid> AccountLinked { get; }
    }
}
