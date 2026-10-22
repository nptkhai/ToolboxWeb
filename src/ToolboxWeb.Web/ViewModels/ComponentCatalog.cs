using ToolboxWeb.Web.Constants;

namespace ToolboxWeb.Web.ViewModels;

/// <summary>Whether a component may be used in ToolboxWeb screens.</summary>
public enum ComponentStatus
{
    /// <summary>In the preferred control set (AGENTS.md) and safe to use on new screens.</summary>
    Usable,

    /// <summary>Shipped with a free library but not adopted; ask before using it on a screen.</summary>
    Available,

    /// <summary>Needs a commercial package (for example @progress/kendo-drawing), so it is not used.</summary>
    CommercialDependency,

    /// <summary>A Kendo UI widget that only exists in the commercial Kendo UI, not in Kendo UI Core.</summary>
    NotInKendoCore
}

/// <summary>A shipped library and the license it is used under (see THIRD-PARTY-NOTICES.md).</summary>
public sealed record ComponentLibrary(string Name, string Version, string License, bool Commercial = false);

/// <summary>
/// One row of the component catalog. <see cref="Family"/> is a component hub tab slug;
/// <see cref="NoteKey"/> is an optional localization key; <see cref="HasDemo"/> marks a live demo on the hub.
/// </summary>
public sealed record ComponentCatalogEntry(
    string Key,
    string Name,
    string Family,
    ComponentLibrary Library,
    ComponentStatus Status,
    string? NoteKey = null,
    bool HasDemo = false);

/// <summary>
/// Single source for the component hub: the catalog tab and the status pill on every demo card read
/// from here, so a status cannot drift between the two. Versions and licenses match THIRD-PARTY-NOTICES.md.
/// </summary>
public static class ComponentCatalog
{
    public const string FoundationsFamily = ToolboxRouteSlugs.ComponentsTabs.Foundations;

    public static readonly ComponentLibrary ToolboxWeb = new("ToolboxWeb", "", "");
    public static readonly ComponentLibrary Bootstrap = new("Bootstrap", "5.3.3", "MIT");
    public static readonly ComponentLibrary KendoCore = new("Kendo UI Core", "2026.2.520", "Apache-2.0");
    public static readonly ComponentLibrary KendoCommercial = new("Kendo UI", "", "", Commercial: true);
    public static readonly ComponentLibrary KendoDrawing = new("@progress/kendo-drawing", "", "", Commercial: true);
    public static readonly ComponentLibrary Tabulator = new("Tabulator", "6.3.1", "MIT");
    public static readonly ComponentLibrary Lucide = new("Lucide", "1.60.0", "ISC");
    public static readonly ComponentLibrary Inter = new("Inter", "5.3.0", "OFL-1.1");
    public static readonly ComponentLibrary ECharts = new("Apache ECharts", "5.6.0", "Apache-2.0");
    public static readonly ComponentLibrary FullCalendar = new("FullCalendar", "6.1.15", "MIT");
    public static readonly ComponentLibrary Quill = new("Quill", "2.0.2", "BSD-3-Clause");
    public static readonly ComponentLibrary SheetJs = new("SheetJS CE", "0.20.2", "Apache-2.0");

    private const string TextInput = ToolboxRouteSlugs.ComponentsTabs.TextInput;
    private const string Selection = ToolboxRouteSlugs.ComponentsTabs.Selection;
    private const string DateTime = ToolboxRouteSlugs.ComponentsTabs.DateTimeNumeric;
    private const string GridData = ToolboxRouteSlugs.ComponentsTabs.GridData;
    private const string Feedback = ToolboxRouteSlugs.ComponentsTabs.FeedbackPopup;
    private const string Layout = ToolboxRouteSlugs.ComponentsTabs.NavigationLayout;

    public static readonly IReadOnlyList<ComponentCatalogEntry> Entries =
    [
        // Foundations: ToolboxWeb's own building blocks (Content/css/components.css, shared partials).
        new("tokens", "Design tokens", FoundationsFamily, ToolboxWeb, ComponentStatus.Usable, HasDemo: true),
        new("font", "Inter", FoundationsFamily, Inter, ComponentStatus.Usable, HasDemo: true),
        new("icons", "Lucide icons", FoundationsFamily, Lucide, ComponentStatus.Usable, HasDemo: true),
        new("buttons", "Buttons", FoundationsFamily, Bootstrap, ComponentStatus.Usable, HasDemo: true),
        new("field", "Form field", FoundationsFamily, ToolboxWeb, ComponentStatus.Usable, HasDemo: true),
        new("pill", "Status pill", FoundationsFamily, ToolboxWeb, ComponentStatus.Usable, HasDemo: true),
        new("chip", "Filter chip", FoundationsFamily, ToolboxWeb, ComponentStatus.Usable, HasDemo: true),
        new("list-row", "List row", FoundationsFamily, ToolboxWeb, ComponentStatus.Usable, HasDemo: true),
        new("states", "Empty, error and loading states", FoundationsFamily, ToolboxWeb, ComponentStatus.Usable, HasDemo: true),
        new("confirm", "Confirm dialog", FoundationsFamily, ToolboxWeb, ComponentStatus.Usable, HasDemo: true),
        new("side-panel", "Side panel", FoundationsFamily, Bootstrap, ComponentStatus.Usable, HasDemo: true),

        // Text input
        new("native-text", "Native text input", TextInput, Bootstrap, ComponentStatus.Usable, HasDemo: true),
        new("native-textarea", "Native textarea", TextInput, Bootstrap, ComponentStatus.Usable, HasDemo: true),
        new("image-upload", "Image upload control", TextInput, ToolboxWeb, ComponentStatus.Usable, HasDemo: true),
        new("file-attachment", "File attachment control", TextInput, ToolboxWeb, ComponentStatus.Usable, HasDemo: true),
        new("kendo-textbox", "Kendo TextBox", TextInput, KendoCore, ComponentStatus.Available, "Components.Catalog.Note.UseNativeInput", HasDemo: true),
        new("kendo-autocomplete", "Kendo AutoComplete", TextInput, KendoCore, ComponentStatus.Usable, HasDemo: true),
        new("kendo-textarea", "Kendo TextArea", TextInput, KendoCore, ComponentStatus.Available, "Components.Catalog.Note.UseNativeInput"),
        new("kendo-floatinglabel", "Kendo FloatingLabel", TextInput, KendoCore, ComponentStatus.Available),
        new("kendo-editor", "Kendo Editor", TextInput, KendoCore, ComponentStatus.Available),
        new("quill", "Quill", TextInput, Quill, ComponentStatus.Available, "Components.Catalog.Note.AssetOnly"),

        // Selection
        new("native-checkbox", "Native checkbox", Selection, Bootstrap, ComponentStatus.Usable, HasDemo: true),
        new("native-select", "Native select", Selection, Bootstrap, ComponentStatus.Usable, HasDemo: true),
        new("kendo-dropdownlist", "Kendo DropDownList", Selection, KendoCore, ComponentStatus.Available, "Components.Catalog.Note.UseComboBox", HasDemo: true),
        new("kendo-combobox", "Kendo ComboBox", Selection, KendoCore, ComponentStatus.Usable, HasDemo: true),
        new("kendo-multiselect", "Kendo MultiSelect", Selection, KendoCore, ComponentStatus.Usable, HasDemo: true),
        new("kendo-listbox", "Kendo ListBox", Selection, KendoCore, ComponentStatus.Available, HasDemo: true),
        new("kendo-checkbox", "Kendo CheckBox", Selection, KendoCore, ComponentStatus.Available, "Components.Catalog.Note.UseNativeInput"),
        new("kendo-buttongroup", "Kendo ButtonGroup", Selection, KendoCore, ComponentStatus.Available),
        new("kendo-togglebutton", "Kendo ToggleButton", Selection, KendoCore, ComponentStatus.Available),
        new("kendo-colorpicker", "Kendo ColorPicker", Selection, KendoCore, ComponentStatus.CommercialDependency, "Components.Catalog.Note.UseColorInput"),

        // Date, time, numeric
        new("native-date", "Native date input", DateTime, Bootstrap, ComponentStatus.Available, "Components.Catalog.Note.UseDatePicker", HasDemo: true),
        new("native-number", "Native number input", DateTime, Bootstrap, ComponentStatus.Available, "Components.Catalog.Note.UseNumericTextBox", HasDemo: true),
        new("kendo-datepicker", "Kendo DatePicker", DateTime, KendoCore, ComponentStatus.Usable, HasDemo: true),
        new("kendo-datetimepicker", "Kendo DateTimePicker", DateTime, KendoCore, ComponentStatus.Available, HasDemo: true),
        new("kendo-timepicker", "Kendo TimePicker", DateTime, KendoCore, ComponentStatus.Available, HasDemo: true),
        new("kendo-numeric", "Kendo NumericTextBox", DateTime, KendoCore, ComponentStatus.Usable, HasDemo: true),
        new("kendo-slider", "Kendo Slider", DateTime, KendoCore, ComponentStatus.Available, HasDemo: true),
        new("kendo-masked", "Kendo MaskedTextBox", DateTime, KendoCore, ComponentStatus.Available, HasDemo: true),
        new("kendo-calendar", "Kendo Calendar", DateTime, KendoCore, ComponentStatus.Available),
        new("kendo-dateinput", "Kendo DateInput", DateTime, KendoCore, ComponentStatus.Available),
        new("fullcalendar", "FullCalendar", DateTime, FullCalendar, ComponentStatus.Available, "Components.Catalog.Note.AssetOnly"),
        new("kendo-scheduler", "Kendo Scheduler", DateTime, KendoCommercial, ComponentStatus.NotInKendoCore, "Components.Catalog.Note.UseFullCalendar"),

        // Grid and data list
        new("tabulator", "Tabulator grid", GridData, Tabulator, ComponentStatus.Usable, HasDemo: true),
        new("kendo-listview", "Kendo DataSource + ListView + Pager", GridData, KendoCore, ComponentStatus.Available, HasDemo: true),
        new("sheetjs", "SheetJS", GridData, SheetJs, ComponentStatus.Usable, "Components.Catalog.Note.SheetJs"),
        new("echarts", "Apache ECharts", GridData, ECharts, ComponentStatus.Usable, "Components.Catalog.Note.ECharts"),
        new("kendo-grid", "Kendo Grid", GridData, KendoCommercial, ComponentStatus.NotInKendoCore, "Components.Catalog.Note.UseTabulator"),
        new("kendo-chart", "Kendo Chart", GridData, KendoCommercial, ComponentStatus.NotInKendoCore, "Components.Catalog.Note.UseECharts"),
        new("kendo-pdf", "Kendo PDF export and drawing", GridData, KendoDrawing, ComponentStatus.CommercialDependency, "Components.Catalog.Note.CommercialDrawing"),

        // Feedback and popup
        new("bootstrap-modal", "Bootstrap modal", Feedback, Bootstrap, ComponentStatus.Usable, HasDemo: true),
        new("bootstrap-alerts", "Bootstrap alerts", Feedback, Bootstrap, ComponentStatus.Usable, HasDemo: true),
        new("kendo-notification", "Kendo Notification", Feedback, KendoCore, ComponentStatus.Usable, HasDemo: true),
        new("kendo-validator", "Kendo Validator", Feedback, KendoCore, ComponentStatus.Available, HasDemo: true),
        new("kendo-window", "Kendo Window", Feedback, KendoCore, ComponentStatus.Available, "Components.Catalog.Note.UseModal", HasDemo: true),
        new("kendo-tooltip", "Kendo Tooltip", Feedback, KendoCore, ComponentStatus.Available, HasDemo: true),
        new("kendo-dialog", "Kendo Dialog and alert helpers", Feedback, KendoCore, ComponentStatus.Available, "Components.Catalog.Note.UseModal"),
        new("kendo-popup", "Kendo Popup", Feedback, KendoCore, ComponentStatus.Available),
        new("kendo-progressbar", "Kendo ProgressBar", Feedback, KendoCore, ComponentStatus.Available),

        // Navigation and layout
        new("bootstrap-tabs", "Bootstrap tabs", Layout, Bootstrap, ComponentStatus.Usable, HasDemo: true),
        new("kendo-menu", "Kendo Menu", Layout, KendoCore, ComponentStatus.Available, HasDemo: true),
        new("kendo-panelbar", "Kendo PanelBar", Layout, KendoCore, ComponentStatus.Available, HasDemo: true),
        new("kendo-splitter", "Kendo Splitter", Layout, KendoCore, ComponentStatus.Available, HasDemo: true),
        new("kendo-tabstrip", "Kendo TabStrip", Layout, KendoCore, ComponentStatus.Available, "Components.Catalog.Note.UseBootstrapTabs"),
        new("kendo-toolbar", "Kendo ToolBar", Layout, KendoCore, ComponentStatus.Available),
        new("kendo-actionsheet", "Kendo ActionSheet", Layout, KendoCore, ComponentStatus.Available),
        new("kendo-responsivepanel", "Kendo ResponsivePanel", Layout, KendoCore, ComponentStatus.Available),
        new("kendo-buttons", "Kendo DropDownButton and SplitButton", Layout, KendoCore, ComponentStatus.Available),
    ];

    private static readonly Dictionary<string, ComponentCatalogEntry> ByKey =
        Entries.ToDictionary(entry => entry.Key, StringComparer.OrdinalIgnoreCase);

    public static ComponentCatalogEntry Get(string key) => ByKey[key];

    /// <summary>Localization key of the status label.</summary>
    public static string StatusLabelKey(ComponentStatus status) => status switch
    {
        ComponentStatus.Usable => "Components.Status.Usable",
        ComponentStatus.Available => "Components.Status.Available",
        ComponentStatus.CommercialDependency => "Components.Status.CommercialDependency",
        _ => "Components.Status.NotInKendoCore"
    };

    /// <summary>Tone class for <c>.tbx-pill</c>.</summary>
    public static string StatusTone(ComponentStatus status) => status switch
    {
        ComponentStatus.Usable => "is-success",
        ComponentStatus.Available => "is-neutral",
        ComponentStatus.CommercialDependency => "is-danger",
        _ => "is-warning"
    };

    /// <summary>Slug used by the catalog filter chips and data attributes.</summary>
    public static string StatusSlug(ComponentStatus status) => status switch
    {
        ComponentStatus.Usable => "usable",
        ComponentStatus.Available => "available",
        ComponentStatus.CommercialDependency => "commercial",
        _ => "not-in-core"
    };
}
