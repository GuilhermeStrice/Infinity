using System.Security.Cryptography.X509Certificates;

namespace Infinity.Http
{
    public class SslSettings
    {
        /// <summary>
        /// Enable or disable SSL.
        /// </summary>
        public bool Enable { get; set; } = false;

        /// <summary>
        /// Certifcate for SSL.
        /// For WatsonWebserver, install the certificate in your operating system.  This property is not used by WatsonWebserver, only WatsonWebserver.Lite.
        /// </summary>
        public X509Certificate2 SslCertificate
        {
            get
            {
                if (ssl_certificate == null)
                {
                    if (!string.IsNullOrEmpty(PfxCertificateFile))
                    {
                        if (!string.IsNullOrEmpty(PfxCertificatePassword))
                        {
                            ssl_certificate = new X509Certificate2(File.ReadAllBytes(PfxCertificateFile), PfxCertificatePassword);
                        }
                        else
                        {
                            ssl_certificate = new X509Certificate2(File.ReadAllBytes(PfxCertificateFile));
                        }
                    }
                }

                return ssl_certificate;
            }
            set
            {
                ssl_certificate = value;
            }
        }

        /// <summary>
        /// PFX certificate filename.
        /// For WatsonWebserver, install the certificate in your operating system.  This property is not used by WatsonWebserver, only WatsonWebserver.Lite.
        /// </summary>
        public string PfxCertificateFile { get; set; } = null;

        /// <summary>
        /// PFX certificate password.
        /// For WatsonWebserver, install the certificate in your operating system.  This property is not used by WatsonWebserver, only WatsonWebserver.Lite.
        /// </summary>
        public string PfxCertificatePassword { get; set; } = null;

        /// <summary>
        /// Require mutual authentication.
        /// This property is not used by WatsonWebserver, only WatsonWebserver.Lite.
        /// </summary>
        public bool MutuallyAuthenticate { get; set; } = false;

        /// <summary>
        /// Accept invalid certificates including self-signed and those that are unable to be verified.
        /// This property is not used by WatsonWebserver, only WatsonWebserver.Lite.
        /// </summary>
        public bool AcceptInvalidAcertificates { get; set; } = true;

        private X509Certificate2 ssl_certificate = null;

        /// <summary>
        /// SSL settings.
        /// </summary>
        public SslSettings()
        {
        }
    }
}
