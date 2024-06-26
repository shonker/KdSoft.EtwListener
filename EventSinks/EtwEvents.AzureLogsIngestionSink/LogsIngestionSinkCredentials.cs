﻿namespace KdSoft.EtwEvents.EventSinks
{
    // See https://learn.microsoft.com/en-us/dotnet/api/azure.identity.environmentcredential?view=azure-dotnet
    public class LogsIngestionSinkCredentials
    {
        public LogsIngestionSinkCredentials() { }

        public LogsIngestionSinkCredentials(string? tenantId = null, string? clientId = null) {
            this.TenantId = tenantId;
            this.ClientId = clientId;
        }

        public string? TenantId { get; set; }
        public string? ClientId { get; set; }

        public ClientSecretSettings? ClientSecret { get; set; }

        public ClientCertificateSettings? ClientCertificate { get; set; }

        public UsernamePasswordSettings? UsernamePassword { get; set; }

        public class ClientSecretSettings
        {
            public string Secret { get; set; } = "";
        }

        public class ClientCertificateSettings
        {
            public ClientCertificateSettings() { }

            public ClientCertificateSettings(ReadOnlySpan<char> certPem) {
                CertificatePem = new string(certPem);
            }

            public ClientCertificateSettings(ReadOnlySpan<char> certPem, ReadOnlySpan<char> keyPem) {
                CertificatePem = new string(certPem);
                CertificateKeyPem = new string(keyPem);
            }

            public ClientCertificateSettings(string? thumbPrint, string? subjectCN) {
                if (thumbPrint == null && subjectCN == null)
                    throw new ArgumentException("Certificate Thumbprint and Subject CN must not both be null.");
                CertificateThumbPrint = thumbPrint;
                CertificateSubjectCN = subjectCN;
            }

            public string? CertificatePem { get; set; }

            public string? CertificateKeyPem { get; set; }

            public string? CertificateThumbPrint { get; set; }

            public string? CertificateSubjectCN { get; set; }
        }

        public class UsernamePasswordSettings(string username, string password)
        {
            public string Username { get; set; } = username;
            public string Password { get; set; } = password;
        }
    }
}
