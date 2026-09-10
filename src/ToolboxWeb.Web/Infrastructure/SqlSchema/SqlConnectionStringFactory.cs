using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using ToolboxWeb.Web.ViewModels.SchemaCompare;

namespace ToolboxWeb.Web.Infrastructure.SqlSchema;

public interface ISqlConnectionStringFactory
{
    /// <summary>
    /// Validates user input and builds a connection string. Throws
    /// <see cref="SqlSchemaValidationException"/> with a user-facing message when the input
    /// is rejected.
    /// </summary>
    string Create(SqlConnectionInputModel input, bool requireDatabase);

    bool HasHostAllowlist { get; }
}

public sealed class SqlConnectionStringFactory : ISqlConnectionStringFactory
{
    private static readonly char[] ConnectionStringDelimiters = [';', '=', '\'', '"'];

    private readonly SqlSchemaOptions _options;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public SqlConnectionStringFactory(IOptions<SqlSchemaOptions> options, IStringLocalizer<SharedResource> localizer)
    {
        _options = options.Value;
        _localizer = localizer;
    }

    public bool HasHostAllowlist => _options.AllowedHosts.Length > 0;

    public string Create(SqlConnectionInputModel input, bool requireDatabase)
    {
        var server = Require(input.Server, "SchemaCompare.Validation.ServerRequired");
        var username = Require(input.Username, "SchemaCompare.Validation.UsernameRequired");

        if (string.IsNullOrEmpty(input.Password))
        {
            throw Invalid("SchemaCompare.Validation.PasswordRequired");
        }

        var database = input.Database?.Trim() ?? string.Empty;
        if (requireDatabase && string.IsNullOrEmpty(database))
        {
            throw Invalid("SchemaCompare.Validation.DatabaseRequired");
        }

        // A full connection string pasted into a single field would silently change settings
        // we deliberately control, so reject the delimiters outright.
        RejectDelimiters(server, "SchemaCompare.Validation.ServerInvalid");
        RejectDelimiters(database, "SchemaCompare.Validation.DatabaseInvalid");
        RejectDelimiters(username, "SchemaCompare.Validation.UsernameInvalid");

        EnsureHostAllowed(server);

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            UserID = username,
            Password = input.Password,
            IntegratedSecurity = false,
            Encrypt = input.Encrypt,
            TrustServerCertificate = input.TrustServerCertificate,
            ConnectTimeout = Math.Clamp(_options.ConnectTimeoutSeconds, 3, 120),
            CommandTimeout = Math.Clamp(_options.CommandTimeoutSeconds, 10, 600),
            ApplicationName = "ToolboxWeb.SchemaCompare",
            Pooling = false,
            MultipleActiveResultSets = false
        };

        if (!string.IsNullOrEmpty(database))
        {
            builder.InitialCatalog = database;
        }

        return builder.ConnectionString;
    }

    /// <summary>
    /// Strips protocol prefix, instance name and port so the bare host can be matched
    /// against the allowlist.
    /// </summary>
    public static string ExtractHost(string server)
    {
        var value = server.Trim();

        var protocolSeparator = value.IndexOf(':');
        if (protocolSeparator > 0 && protocolSeparator <= 4)
        {
            value = value[(protocolSeparator + 1)..];
        }

        var instanceSeparator = value.IndexOf('\\');
        if (instanceSeparator >= 0)
        {
            value = value[..instanceSeparator];
        }

        var portSeparator = value.IndexOf(',');
        if (portSeparator >= 0)
        {
            value = value[..portSeparator];
        }

        return value.Trim();
    }

    private void EnsureHostAllowed(string server)
    {
        if (!HasHostAllowlist)
        {
            return;
        }

        var host = ExtractHost(server);
        var allowed = _options.AllowedHosts.Any(x => string.Equals(x.Trim(), host, StringComparison.OrdinalIgnoreCase));
        if (!allowed)
        {
            throw new SqlSchemaValidationException(_localizer["SchemaCompare.Validation.HostNotAllowed", host].Value);
        }
    }

    private string Require(string? value, string localizationKey)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw Invalid(localizationKey);
        }

        return trimmed;
    }

    private void RejectDelimiters(string value, string localizationKey)
    {
        if (value.IndexOfAny(ConnectionStringDelimiters) >= 0)
        {
            throw Invalid(localizationKey);
        }
    }

    private SqlSchemaValidationException Invalid(string localizationKey)
    {
        return new SqlSchemaValidationException(_localizer[localizationKey].Value);
    }
}
