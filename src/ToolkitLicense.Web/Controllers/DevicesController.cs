using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolkitLicense.Web.Infrastructure;
using ToolkitLicense.Web.Services;
using ToolkitLicense.Web.ViewModels;

namespace ToolkitLicense.Web.Controllers;

/// <summary>Devices and their licenses: assign to a customer, re-issue or renew, revoke, note.</summary>
public sealed class DevicesController(LicenseService licenses, CustomerService customers, ReleaseService releases, IStringLocalizer<SharedResource> T) : Controller
{
    public async Task<IActionResult> Index(string? q) => View(new DevicesViewModel
    {
        Search = q,
        Devices = await licenses.DevicesAsync(q),
        LatestVersion = (await releases.LatestAsync())?.Version
    });

    public async Task<IActionResult> Details(string id)
    {
        var device = await licenses.DeviceAsync(id);
        if (device is null)
        {
            return NotFound();
        }

        return View(new DeviceDetailsViewModel
        {
            Device = device,
            Active = await licenses.ActiveLicenseAsync(id),
            Licenses = device.Licenses.OrderByDescending(x => x.IssuedAtUtc).ToList(),
            Requests = await licenses.RequestsForDeviceAsync(id),
            Customers = await customers.AllAsync(),
            KeyThumbprint = WireFormat.KeyThumbprint(device.PublicKey),
            LatestVersion = (await releases.LatestAsync())?.Version
        });
    }

    [HttpPost]
    public async Task<IActionResult> Assign(string id, int? customerId) =>
        Done(id, await licenses.AssignAsync(id, customerId), "Device.Assigned");

    [HttpPost]
    public async Task<IActionResult> Note(string id, string? note) =>
        Done(id, await licenses.SetNoteAsync(id, note), "Device.NoteSaved");

    [HttpPost]
    public async Task<IActionResult> Reissue(string id, LicenseTerm term, DateTime? date) =>
        Done(id, await licenses.ReissueAsync(id, new ReissueInput(term, date)), "Device.Reissued");

    [HttpPost]
    public async Task<IActionResult> Revoke(string id, string? reason) =>
        Done(id, await licenses.RevokeAsync(id, reason), "Device.Revoked");

    private IActionResult Done(string id, string? error, string successKey)
    {
        TempData[error is null ? "Success" : "Error"] = T[error ?? successKey].Value;
        return RedirectToAction(nameof(Details), new { id });
    }
}
