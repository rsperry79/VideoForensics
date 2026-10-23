using Certes;
using Certes.Acme;
using Certes.Acme.Resource;

namespace VideoForensics.Hosting
{
    /// <summary>Publishes and removes the TXT record a DNS-01 challenge needs.</summary>
    public interface IDnsChallengePublisher
    {
        /// <summary>Publishes a TXT record and returns its id.</summary>
        Task<string> PublishTxtAsync(string recordName, string value, CancellationToken ct);

        /// <summary>Removes a record previously returned by <see cref="PublishTxtAsync"/>.</summary>
        Task RemoveAsync(string recordId, CancellationToken ct);
    }

    /// <summary>
    /// ACME account state kept between issuances so renewals reuse the same Let's Encrypt account.
    /// </summary>
    public sealed record AcmeAccountState(string AccountKeyPem);

    /// <summary>
    /// A certificate chain with its private key, as one PEM bundle. <see cref="NotAfter"/> is the leaf expiry.
    /// </summary>
    public sealed record IssuedCertificate(string PemBundle, DateTimeOffset NotAfter, string AccountKeyPem);

    public interface IAcmeCertificateIssuer
    {
        /// <summary>
        /// Runs a DNS-01 order for <paramref name="domain"/>. The TXT record is always removed afterwards,
        /// whether issuance succeeded or not.
        /// </summary>
        Task<IssuedCertificate> IssueAsync(
            string domain,
            string contactEmail,
            AcmeAccountState? account,
            bool useStaging,
            IDnsChallengePublisher dns,
            CancellationToken ct);
    }

    /// <summary>
    /// Let's Encrypt issuance through Certes. Staging is the default for the caller; production issues trusted
    /// certificates and is rate-limited, so it should only be used after a staging run succeeds.
    /// </summary>
    public sealed class CertesAcmeCertificateIssuer : IAcmeCertificateIssuer
    {
        // Let's Encrypt validates against public DNS, so wait for the record to spread before asking it to check.
        private static readonly TimeSpan PropagationDelay = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
        private const int MaxPolls = 40;

        public async Task<IssuedCertificate> IssueAsync(
            string domain,
            string contactEmail,
            AcmeAccountState? account,
            bool useStaging,
            IDnsChallengePublisher dns,
            CancellationToken ct)
        {
            IKey accountKey = account is null
                ? KeyFactory.NewKey(KeyAlgorithm.ES256)
                : KeyFactory.FromPem(account.AccountKeyPem);
            Uri server = useStaging ? WellKnownServers.LetsEncryptStagingV2 : WellKnownServers.LetsEncryptV2;

            var acme = new AcmeContext(server, accountKey);
            _ = await acme.NewAccount(contactEmail, true);

            IOrderContext order = await acme.NewOrder([domain]);
            foreach (IAuthorizationContext authorization in await order.Authorizations())
            {
                await CompleteDnsChallengeAsync(acme, order, authorization, domain, dns, ct);
            }

            IKey certificateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
            CertificateChain chain = await order.Generate(new CsrInfo { CommonName = domain }, certificateKey);
            string pem = chain.ToPem(certificateKey);

            using var leaf = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(pem);
            return new IssuedCertificate(pem, leaf.NotAfter, acme.AccountKey.ToPem());
        }

        private static async Task CompleteDnsChallengeAsync(
            AcmeContext acme,
            IOrderContext order,
            IAuthorizationContext authorization,
            string domain,
            IDnsChallengePublisher dns,
            CancellationToken ct)
        {
            IChallengeContext challenge = await authorization.Dns();
            string token = (await challenge.Resource()).Token;
            string txtValue = acme.AccountKey.DnsTxt(token);

            string recordId = await dns.PublishTxtAsync($"_acme-challenge.{domain}", txtValue, ct);
            try
            {
                await Task.Delay(PropagationDelay, ct);
                _ = await challenge.Validate();

                for (int poll = 0; poll < MaxPolls; poll++)
                {
                    Order current = await order.Resource();
                    if (current.Status is OrderStatus.Ready or OrderStatus.Valid)
                    {
                        return;
                    }

                    if (current.Status == OrderStatus.Invalid)
                    {
                        throw new InvalidOperationException("Let's Encrypt rejected the DNS challenge for " + domain + ".");
                    }

                    await Task.Delay(PollInterval, ct);
                }

                throw new TimeoutException("Let's Encrypt did not validate the DNS challenge for " + domain + " in time.");
            }
            finally
            {
                await dns.RemoveAsync(recordId, CancellationToken.None);
            }
        }
    }
}
