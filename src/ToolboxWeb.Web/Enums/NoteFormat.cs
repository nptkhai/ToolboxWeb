namespace ToolboxWeb.Web.Enums;

/// <summary>
/// What kind of content a note holds. Stored as its integer value (the repo does not use
/// <c>HasConversion</c>), so existing numbers must never change; only append new ones.
/// </summary>
public enum NoteFormat
{
    Text = 1,
    JavaScript = 2,
    Css = 3,
    Json = 4,
    Xml = 5,
    Link = 6,
    Server = 7,

    /// <summary>A group whose sub-notes each choose their own format.</summary>
    Mixed = 8,

    /// <summary>A list of tasks, each row with a state of its own.</summary>
    Checklist = 9
}
