using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Enums;

namespace ToolboxWeb.Web.ViewModels;

public class NoteFormViewModel : IValidatableObject
{
    public int? Id { get; set; }

    /// <summary>Set when the note is a sub-note of this group.</summary>
    public int? ParentId { get; set; }

    [Required, StringLength(160)]
    public string Title { get; set; } = string.Empty;

    // Optional: a group may hold nothing but sub-notes. Capped so a single paste cannot bloat
    // the database.
    [StringLength(200_000)]
    public string? Content { get; set; }

    [StringLength(250)]
    public string? Tags { get; set; }

    public bool IsPinned { get; set; }

    public NoteFormat Format { get; set; } = NoteFormat.Text;

    // Connection details: only a sub-note of the Server format carries them.
    [StringLength(200)]
    public string? Host { get; set; }

    [Range(1, 65535)]
    public int? Port { get; set; }

    [StringLength(128)]
    public string? Username { get; set; }

    [StringLength(128)]
    public string? Database { get; set; }

    /// <summary>Inbound only; the stored password is never sent back. Empty keeps the current one.</summary>
    [StringLength(512), DataType(DataType.Password)]
    public string? NewPassword { get; set; }

    public bool ClearPassword { get; set; }

    // Row editors post one value per row, in the order the rows appear on screen. A name and an
    // address belong to the same link row by position, as do a text and a state in a checklist.
    public List<string>? LinkNames { get; set; }

    public List<string>? LinkUrls { get; set; }

    public List<string>? Items { get; set; }

    public List<string>? ItemStatuses { get; set; }

    // Where to come back to after the post: the filter the user was looking at.
    public string? ReturnFormat { get; set; }
    public string? ReturnSearch { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var localizer = validationContext.GetService(typeof(IStringLocalizer<SharedResource>)) as IStringLocalizer<SharedResource>;

        if (!Enum.IsDefined(Format))
        {
            yield return new ValidationResult(
                localizer?["Notes.InvalidFormat"].Value ?? "Unknown format.",
                [nameof(Format)]);
        }

        // Only a connection needs a host. A server group is just a title and a description;
        // its connections are the sub-notes.
        if (Format == NoteFormat.Server && ParentId is not null && string.IsNullOrWhiteSpace(Host))
        {
            yield return new ValidationResult(
                localizer?["Notes.Server.HostRequired"].Value ?? "Enter a host.",
                [nameof(Host)]);
        }
    }
}
