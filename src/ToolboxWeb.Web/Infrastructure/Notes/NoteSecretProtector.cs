using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace ToolboxWeb.Web.Infrastructure.Notes;

/// <summary>
/// Encrypts the password stored on a Server-format note.
/// <para>
/// Built on ASP.NET Data Protection, whose key ring is already persisted by Program.cs, so a
/// value protected today can still be read after a restart. Each user gets their own
/// sub-purpose: ciphertext copied from one user's row cannot be decrypted in another user's
/// context even if both rows sit in the same database.
/// </para>
/// <para>
/// What this does and does not protect: a leaked or carelessly backed-up app.db no longer
/// reveals passwords. Someone who also has the key ring folder can still decrypt them, because
/// the key ring itself is not encrypted at rest — that is an app-wide setting, not this feature's.
/// </para>
/// </summary>
public interface INoteSecretProtector
{
    string Protect(string userId, string plaintext);

    /// <summary>
    /// Returns null instead of throwing when the value cannot be decrypted — typically because
    /// the database was copied from a machine with a different key ring.
    /// </summary>
    string? Unprotect(string userId, string protectedValue);
}

public sealed class NoteSecretProtector : INoteSecretProtector
{
    // Versioned so a future change of scheme can coexist with rows written by this one.
    private const string Purpose = "ToolboxWeb.Notes.ServerPassword.v1";

    private readonly IDataProtectionProvider _provider;

    public NoteSecretProtector(IDataProtectionProvider provider)
    {
        _provider = provider;
    }

    public string Protect(string userId, string plaintext) =>
        _provider.CreateProtector(Purpose, userId).Protect(plaintext);

    public string? Unprotect(string userId, string protectedValue)
    {
        try
        {
            return _provider.CreateProtector(Purpose, userId).Unprotect(protectedValue);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
