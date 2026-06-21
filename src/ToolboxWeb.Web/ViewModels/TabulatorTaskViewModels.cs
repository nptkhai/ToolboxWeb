using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ToolboxWeb.Web.Enums;

namespace ToolboxWeb.Web.ViewModels;

public class TabulatorTaskDto
{
    public int Id { get; set; }
    public string RowKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CategoryLabel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string PriorityLabel { get; set; } = string.Empty;
    public int Progress { get; set; }
    public decimal Budget { get; set; }
    public string? DueDate { get; set; }
    public string? Note { get; set; }
    public string UpdatedAt { get; set; } = string.Empty;
}

public class TabulatorTaskInputViewModel
{
    public int? Id { get; set; }

    [Required]
    [StringLength(180)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [EnumDataType(typeof(TabulatorTaskCategory))]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TabulatorTaskCategory Category { get; set; }

    [Required]
    [EnumDataType(typeof(TabulatorTaskStatus))]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TabulatorTaskStatus Status { get; set; }

    [Required]
    [EnumDataType(typeof(TabulatorTaskPriority))]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TabulatorTaskPriority Priority { get; set; }

    [Range(0, 100)]
    public int Progress { get; set; }

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal Budget { get; set; }

    public DateTime? DueDate { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }
}

public class TabulatorTaskBatchSaveRequest
{
    public List<TabulatorTaskInputViewModel> Created { get; set; } = [];
    public List<TabulatorTaskInputViewModel> Updated { get; set; } = [];
    public List<int> DeletedIds { get; set; } = [];
}

public class TabulatorTaskBatchSaveResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = [];
    public IReadOnlyList<TabulatorTaskDto> Items { get; set; } = [];
}
