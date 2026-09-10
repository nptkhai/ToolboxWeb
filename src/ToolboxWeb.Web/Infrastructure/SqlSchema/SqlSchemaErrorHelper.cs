using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace ToolboxWeb.Web.Infrastructure.SqlSchema;

public static partial class SqlSchemaErrorHelper
{
    /// <summary>
    /// Turns a connection or query failure into something safe to show. Never let a raw
    /// exception message reach the browser: it can carry the connection string.
    /// </summary>
    public static string ToUserMessage(
        Exception exception,
        string fallback,
        string? loginFailed = null,
        string? serverNotFound = null,
        string? databaseNotFound = null,
        string? permissionDenied = null,
        string? timeout = null)
    {
        // Validation failures are authored by this module and safe to show verbatim.
        if (exception is SqlSchemaValidationException validation)
        {
            return validation.Message;
        }

        // DacFx wraps the real cause: "Could not extract package from specified database"
        // with the actual SqlException several levels down. Without unwrapping, every DacFx
        // failure would show the same unhelpful fallback.
        if (exception is not SqlException && FindSqlException(exception) is { } wrapped)
        {
            exception = wrapped;
        }

        if (exception is SqlException sql)
        {
            var byNumber = sql.Number switch
            {
                18456 or 18452 => loginFailed,
                4060 or 911 or 916 => databaseNotFound,
                53 or 87 or 11001 or 40615 => serverNotFound,
                229 or 230 or 297 => permissionDenied,
                -2 => timeout,
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(byNumber))
            {
                return byNumber;
            }

            // Connection-open failures surface as number 0 or 20 with a wrapped Win32 error.
            if (sql.Number is 0 or 20 or 26 && !string.IsNullOrWhiteSpace(serverNotFound))
            {
                return serverNotFound;
            }

            return $"{fallback} (SQL {sql.Number})";
        }

        if (exception is TimeoutException && !string.IsNullOrWhiteSpace(timeout))
        {
            return timeout;
        }

        // A Win32 "wait operation timed out" (258) can arrive without a SqlException wrapper.
        if (!string.IsNullOrWhiteSpace(timeout) && MentionsTimeout(exception))
        {
            return timeout;
        }

        return fallback;
    }

    private static SqlException? FindSqlException(Exception exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql)
            {
                return sql;
            }

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    if (inner is SqlException innerSql)
                    {
                        return innerSql;
                    }
                }
            }
        }

        return null;
    }

    private static bool MentionsTimeout(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is System.ComponentModel.Win32Exception { NativeErrorCode: 258 })
            {
                return true;
            }

            if (current.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("Timeout Expired", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes anything that looks like a password from text before it is logged.
    /// </summary>
    public static string Scrub(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return SecretPattern().Replace(text, "$1=***");
    }

    [GeneratedRegex(@"\b(password|pwd|user id|uid)\s*=\s*[^;]*", RegexOptions.IgnoreCase)]
    private static partial Regex SecretPattern();
}

/// <summary>Input rejected before any connection is attempted. Message is user-facing.</summary>
public sealed class SqlSchemaValidationException : Exception
{
    public SqlSchemaValidationException(string message) : base(message)
    {
    }
}
