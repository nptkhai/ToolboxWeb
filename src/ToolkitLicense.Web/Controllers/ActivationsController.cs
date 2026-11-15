using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolkitLicense.Web.Domain;
using ToolkitLicense.Web.Infrastructure;
using ToolkitLicense.Web.Services;
using ToolkitLicense.Web.ViewModels;

namespace ToolkitLicense.Web.Controllers;

/// <summary>Requests from devices: approve with a customer and a term, reject, or paste a token by hand.</summary>
public sealed class ActivationsController(LicenseService licenses, CustomerService customers, IStringLocalizer<SharedResource> T) : Controller
{
    public async Task<IActionResult> Index() => View(await BuildAsync());

    /// <summary>A token sent by chat or mail: becomes a request like an online one.</summary>
    [HttpPost]
    public async Task<IActionResult> Manual(string? token)
    {
        var result = await licenses.SubmitAsync(token, ActivationSource.Manual, HttpContext.Connection.RemoteIpAddress?.ToString());
        if (result.Request is null)
        {
            var model = await BuildAsync();
            model.Token = token;
            model.TokenError = T[$"Token.Error.{result.Error}"].Value;
            return View(nameof(Index), model);
        }

        if (result.Request.Status == ActivationStatus.Approved)
        {
            TempData["Info"] = T["Activation.AlreadyLicensed"].Value;
        }

        return RedirectToAction(nameof(Details), new { id = result.Request.Id });
    }

    public async Task<IActionResult> Details(string id)
    {
        var model = await BuildDetailsAsync(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Approve(string id, int? customerId, string? newCustomerName, LicenseTerm term, DateTime? date, string? note)
    {
        var error = await licenses.ApproveAsync(id, new ApproveInput(customerId, newCustomerName, term, date, note));
        if (error is not null)
        {
            TempData["Error"] = T[error].Value;
        }
        else
        {
            TempData["Success"] = T["Activation.Approved"].Value;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Reject(string id, string? note)
    {
        var error = await licenses.RejectAsync(id, note);
        TempData[error is null ? "Success" : "Error"] = T[error ?? "Activation.Rejected"].Value;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<ActivationsViewModel> BuildAsync() => new()
    {
        Pending = await licenses.PendingAsync(),
        History = await licenses.HistoryAsync(50)
    };

    private async Task<ActivationDetailsViewModel?> BuildDetailsAsync(string id)
    {
        var request = await licenses.GetRequestAsync(id);
        if (request?.Device is null)
        {
            return null;
        }

        var (_, license) = await licenses.PollAsync(id);
        return new ActivationDetailsViewModel
        {
            Request = request,
            License = request.Status == ActivationStatus.Approved ? license : null,
            Customers = await customers.AllAsync(),
            CustomerId = request.Device.CustomerId,
            KeyThumbprint = WireFormat.KeyThumbprint(request.Device.PublicKey)
        };
    }
}
