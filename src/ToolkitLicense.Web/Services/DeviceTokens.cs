using System.Text.RegularExpressions;
using ToolkitLicense.Web.Infrastructure;

namespace ToolkitLicense.Web.Services;

/// <summary>Why a token or heartbeat was refused. The API sends the code; the admin page shows "Token.Error.{code}".</summary>
public enum TokenError
{
    None,
    Malformed,
    BadSignature,
    BadDevice,
    KeyMismatch,
    UnknownDevice,
    Stale
}

/// <summary>Reads and checks the activation token a device made (TKT1, see WireFormat).</summary>
public static partial class DeviceTokens
{
    public static (DeviceToken? Token, TokenError Error) Read(string? text)
    {
        if (!WireFormat.TryDecode(text, WireFormat.TokenPrefix, out var payload, out var signature))
        {
            return (null, TokenError.Malformed);
        }

        var token = WireFormat.Read<DeviceToken>(payload);
        if (token is null || token.V != 1 || string.IsNullOrWhiteSpace(token.PublicKey))
        {
            return (null, TokenError.Malformed);
        }

        if (!DeviceIdPattern().IsMatch(token.DeviceId ?? string.Empty))
        {
            return (null, TokenError.BadDevice);
        }

        if (!WireFormat.Verify(token.PublicKey, payload, signature))
        {
            return (null, TokenError.BadSignature);
        }

        return (token with
        {
            MachineName = Clip(token.MachineName, 120),
            WindowsUser = Clip(token.WindowsUser, 120),
            AppVersion = Clip(token.AppVersion, 40)
        }, TokenError.None);
    }

    public static bool IsDeviceId(string? id) => DeviceIdPattern().IsMatch(id ?? string.Empty);

    public static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length > max ? text[..max] : text;
    }

    [GeneratedRegex("^[A-Za-z0-9-]{8,64}$")]
    private static partial Regex DeviceIdPattern();
}
