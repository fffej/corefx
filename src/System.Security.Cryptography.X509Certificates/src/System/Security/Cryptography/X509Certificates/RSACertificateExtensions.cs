﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Internal.Cryptography;
using Internal.Cryptography.Pal;

namespace System.Security.Cryptography.X509Certificates
{
    /// <summary>
    /// Provides extension methods for retrieving <see cref="RSA" /> implementations for the
    /// public and private keys of a <see cref="X509Certificate2" />.
    /// </summary>
    public static class RSACertificateExtensions
    {
        /// <summary>
        /// Gets the <see cref="RSA" /> public key from the certificate or null if the certificate does not have an RSA public key.
        /// </summary>
        [SecuritySafeCritical]
        public static RSA GetRSAPublicKey(this X509Certificate2 certificate)
        {
            if (certificate == null)
                throw new ArgumentNullException("certificate");

            if (!IsRSA(certificate))
            {
                return null;
            }

            PublicKey publicKey = certificate.PublicKey;

            // When the CNG contract comes online this needs to return an RsaCng on Windows, unless CNG reports
            // an error, then try falling back to CAPI (for CAPI-only smartcards).
            return (RSA)X509Pal.Instance.DecodePublicKey(
                publicKey.Oid,
                publicKey.EncodedKeyValue.RawData,
                publicKey.EncodedParameters.RawData);
        }

        /// <summary>
        /// Gets the <see cref="RSA" /> private key from the certificate or null if the certificate does not have an RSA private key.
        /// </summary>
        [SecuritySafeCritical]
        public static RSA GetRSAPrivateKey(this X509Certificate2 certificate)
        {
            if (certificate == null)
                throw new ArgumentNullException("certificate");

            if (!certificate.HasPrivateKey || !IsRSA(certificate))
            {
                return null;
            }

            // When the CNG contract comes online this needs to return an RsaCng on Windows, unless CNG reports
            // an error, then try falling back to CAPI (for CAPI-only smartcards).
            return certificate.Pal.GetRSAPrivateKey();
        }

        private static bool IsRSA(X509Certificate2 certificate)
        {
            Oid algorithmOid = certificate.PublicKey.Oid;

            switch (algorithmOid.Value)
            {
                case Oids.RsaRsa:
                    return true;
                default:
                    return false;
            }
        }
    }
}
