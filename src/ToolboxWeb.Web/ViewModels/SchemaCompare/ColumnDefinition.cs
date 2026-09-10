using System.Globalization;

namespace ToolboxWeb.Web.ViewModels.SchemaCompare;

public sealed class ColumnDefinition
{
    public required string Name { get; init; }
    public int Ordinal { get; init; }

    /// <summary>Base system type name, for example <c>nvarchar</c>.</summary>
    public required string DataType { get; init; }

    /// <summary>Raw <c>sys.columns.max_length</c> in bytes. -1 means MAX.</summary>
    public int MaxLength { get; init; }

    public byte Precision { get; init; }
    public byte Scale { get; init; }
    public bool IsNullable { get; init; }
    public string? Collation { get; init; }

    public bool IsIdentity { get; init; }
    public string? IdentitySeed { get; init; }
    public string? IdentityIncrement { get; init; }

    public string? ComputedDefinition { get; init; }
    public bool IsComputedPersisted { get; init; }

    public string? DefaultName { get; init; }
    public string? DefaultDefinition { get; init; }

    /// <summary>True when SQL Server invented the default constraint name, e.g. DF__T__C__1A2B3C4D.</summary>
    public bool DefaultIsSystemNamed { get; init; }

    /// <summary>Type rendered the way it appears in DDL, for example <c>nvarchar(100)</c>.</summary>
    public string TypeDisplay => BuildTypeDisplay();

    private string BuildTypeDisplay()
    {
        var type = DataType.ToLowerInvariant();

        switch (type)
        {
            case "char":
            case "varchar":
            case "binary":
            case "varbinary":
                return $"{type}({FormatLength(MaxLength)})";

            case "nchar":
            case "nvarchar":
                return $"{type}({FormatLength(MaxLength < 0 ? MaxLength : MaxLength / 2)})";

            case "decimal":
            case "numeric":
                return string.Create(CultureInfo.InvariantCulture, $"{type}({Precision},{Scale})");

            case "datetime2":
            case "datetimeoffset":
            case "time":
                return string.Create(CultureInfo.InvariantCulture, $"{type}({Scale})");

            default:
                return type;
        }
    }

    private static string FormatLength(int length)
    {
        return length < 0 ? "max" : length.ToString(CultureInfo.InvariantCulture);
    }
}
