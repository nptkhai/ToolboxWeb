using System.Security.Cryptography;

namespace ToolkitLicense.Web.Infrastructure;

/// <summary>
/// First start: while no administrator exists, creating one needs a one-time code written to
/// setup-code.txt in the data folder, which only someone with access to the server can read. The
/// file is deleted once the administrator is created.
/// </summary>
public sealed class SetupCode
{
    private readonly LicenseStorage _storage;

    public SetupCode(LicenseStorage storage)
    {
        _storage = storage;
    }

    public string FilePath => _storage.SetupCodePath;

    public void EnsureCreated(ILogger logger)
    {
        if (File.Exists(FilePath))
        {
            return;
        }

        var code = string.Join('-', Enumerable.Range(0, 4).Select(_ => RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 4)));
        File.WriteAllText(FilePath, code);
        logger.LogWarning("No administrator yet. Open /setup and enter the code from {Path}.", FilePath);
    }

    public bool Matches(string? code)
    {
        if (!File.Exists(FilePath) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var expected = File.ReadAllText(FilePath).Trim();
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(expected),
            System.Text.Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant()));
    }

    public void Delete()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }
}
