using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToolkitLicense.Web.Data;
using ToolkitLicense.Web.ViewModels;

namespace ToolkitLicense.Web.Controllers;

public sealed class AuditController(LicenseDbContext db) : Controller
{
    private const int PageSize = 50;

    public async Task<IActionResult> Index(int page = 1)
    {
        page = Math.Max(1, page);
        var entries = await db.AuditEntries.OrderByDescending(x => x.Id).Skip((page - 1) * PageSize).Take(PageSize + 1).ToListAsync();
        return View(new AuditViewModel { Entries = entries.Take(PageSize).ToList(), Page = page, HasMore = entries.Count > PageSize });
    }
}
