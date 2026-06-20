using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace ToolboxWeb.Web.Controllers;

public class CultureController : Controller
{
    private static readonly HashSet<string> SupportedCultures = new(StringComparer.OrdinalIgnoreCase)
    {
        "vi-VN",
        "en-US"
    };

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Set(string culture, string? returnUrl = null)
    {
        var selectedCulture = SupportedCultures.Contains(culture) ? culture : "vi-VN";
        var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(selectedCulture));

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            cookieValue,
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            });

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Index", "Dashboard");
    }
}
