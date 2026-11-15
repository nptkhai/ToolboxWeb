using System.Security.Cryptography;

namespace ToolkitLicense.Web.Infrastructure;

/// <summary>
/// The server's ECDSA P-256 key that signs every license. Created on the first start and kept in
/// signing-key.pem in the data folder: back it up. Losing it means new licenses can no longer be
/// signed with the key the desktop app trusts; a leaked copy lets anyone sign licenses.
/// The public half is built into the desktop app (M4) and shown on the "Signing key" page.
/// </summary>
public sealed class SigningKey : IDisposable
{
    private readonly ECDsa _key;

    public SigningKey(LicenseStorage storage, ILogger<SigningKey> logger)
    {
        _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        if (File.Exists(storage.SigningKeyPath))
        {
            _key.ImportFromPem(File.ReadAllText(storage.SigningKeyPath));
        }
        else
        {
            File.WriteAllText(storage.SigningKeyPath, _key.ExportECPrivateKeyPem());
            logger.LogWarning("Created a new license signing key at {Path}. Back it up.", storage.SigningKeyPath);
        }

        var publicKey = _key.ExportSubjectPublicKeyInfo();
        PublicKeyBase64 = Convert.ToBase64String(publicKey);
        PublicKeyPem = _key.ExportSubjectPublicKeyInfoPem();
        Fingerprint = Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant();
    }

    /// <summary>SubjectPublicKeyInfo, base64: what the desktop app embeds to check licenses.</summary>
    public string PublicKeyBase64 { get; }
    public string PublicKeyPem { get; }

    /// <summary>SHA-256 of the public key, hex; shown so the key in the app can be compared.</summary>
    public string Fingerprint { get; }

    /// <summary>ECDSA P-256 / SHA-256 signature in the fixed r||s form (IEEE P1363).</summary>
    public byte[] Sign(byte[] data) => _key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

    public void Dispose() => _key.Dispose();
}
