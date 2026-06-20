using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Enums;

namespace ToolboxWeb.Web.Extensions;

public static class EnumLocalizationExtensions
{
    public static string Localize(this ActivityActionType value, IStringLocalizer localizer)
    {
        return localizer[$"ActivityActionType.{value}"].Value;
    }

    public static string Localize(this ActivityEntityType value, IStringLocalizer localizer)
    {
        return localizer[$"ActivityEntityType.{value}"].Value;
    }

    public static string Localize(this ChecklistStatus value, IStringLocalizer localizer)
    {
        return localizer[$"ChecklistStatus.{value}"].Value;
    }

    public static string Localize(this PromptCategory value, IStringLocalizer localizer)
    {
        return localizer[$"PromptCategory.{value}"].Value;
    }
}
