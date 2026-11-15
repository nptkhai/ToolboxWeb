using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ToolkitLicense.Web.Infrastructure;

/// <summary>
/// The texts exchanged with the desktop app. Each is "PREFIX.payload.signature": the payload is
/// UTF-8 JSON (camelCase) in base64url, the signature is ECDSA P-256 / SHA-256 over the payload
/// bytes in r||s form (IEEE P1363), base64url. The full description is in docs/LICENSE_SERVER.md.
/// <list type="bullet">
/// <item><c>TKT1</c> activation token: made and signed by the device with its own key.</item>
/// <item><c>TKL1</c> license: made here and signed with the server key (SigningKey).</item>
/// <item><c>TKH1</c> heartbeat: the device reports its version, signed with its key.</item>
/// </list>
/// </summary>
public static class WireFormat
{
    public const string TokenPrefix = "TKT1";
    public const string LicensePrefix = "TKL1";
    public const string HeartbeatPrefix = "TKH1";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Encode(string prefix, object payload, Func<byte[], byte[]> sign)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, Json);
        return $"{prefix}.{Base64Url(bytes)}.{Base64Url(sign(bytes))}";
    }

    /// <summary>Splits and decodes a text; the signature is checked by the caller with the right key.</summary>
    public static bool TryDecode(string? text, string prefix, out byte[] payload, out byte[] signature)
    {
        payload = [];
        signature = [];
        var parts = (text ?? string.Empty).Trim().Split('.');
        if (parts.Length != 3 || parts[0] != prefix)
        {
            return false;
        }

        try
        {
            payload = FromBase64Url(parts[1]);
            signature = FromBase64Url(parts[2]);
            return payload.Length > 0 && signature.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool Verify(string publicKeyBase64, byte[] payload, byte[] signature)
    {
        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
            return key.KeySize == 256
                && key.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return false;
        }
    }

    public static string KeyThumbprint(string publicKeyBase64) =>
        Base64Url(SHA256.HashData(Convert.FromBase64String(publicKeyBase64)));

    public static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromBase64Url(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }

    public static T? Read<T>(byte[] payload)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(payload), Json);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}

/// <summary>TKT1 payload, made by the device (M4).</summary>
public sealed record DeviceToken(int V, string DeviceId, string MachineName, string WindowsUser, string AppVersion, string PublicKey, DateTimeOffset CreatedAt);

/// <summary>TKL1 payload. The device checks the signature, its own id and key, and the expiry.</summary>
public sealed record LicensePayload(
    int V,
    string LicenseId,
    string DeviceId,
    string DeviceKey,
    string Customer,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt);

/// <summary>TKH1 payload: sent by the device now and then; answered with the license status and the latest release.</summary>
public sealed record HeartbeatPayload(int V, string DeviceId, string AppVersion, string? LicenseId, DateTimeOffset SentAt);
