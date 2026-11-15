using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToolkitLicense.Web.Data;
using ToolkitLicense.Web.Services;
using ToolkitLicense.Web.ViewModels;

namespace ToolkitLicense.Web.Controllers;

public sealed class HomeController(LicenseService licenses, ReleaseService releases, LicenseDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var (devices, active, seen) = await licenses.CountsAsync();
        var inUse = await releases.VersionsInUseAsync();
        return View(new OverviewViewModel
        {
            Pending = await licenses.PendingCountAsync(),
            Devices = devices,
            ActiveLicenses = active,
            SeenThisWeek = seen,
            Latest = await releases.LatestAsync(),
            DevicesBehind = inUse.Where(x => x.Behind).Sum(x => x.Devices),
            PendingRequests = (await licenses.PendingAsync()).Take(5).ToList(),
            RecentAudit = await db.AuditEntries.OrderByDescending(x => x.Id).Take(6).ToListAsync()
        });
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
