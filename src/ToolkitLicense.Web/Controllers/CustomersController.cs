using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolkitLicense.Web.Services;
using ToolkitLicense.Web.ViewModels;

namespace ToolkitLicense.Web.Controllers;

public sealed class CustomersController(CustomerService customers, IStringLocalizer<SharedResource> T) : Controller
{
    public async Task<IActionResult> Index() => View(new CustomersViewModel { Customers = await customers.AllAsync() });

    [HttpPost]
    public async Task<IActionResult> Create(CustomersViewModel input)
    {
        var error = await customers.CreateAsync(new CustomerInput(input.Name, input.Email, input.Note));
        if (error is not null)
        {
            ModelState.AddModelError(error == "Customer.Error.Email" ? nameof(input.Email) : nameof(input.Name), T[error]);
            input.Customers = await customers.AllAsync();
            return View(nameof(Index), input);
        }

        TempData["Success"] = T["Customer.Created", input.Name!.Trim()].Value;
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var customer = await customers.GetAsync(id);
        if (customer is null)
        {
            return NotFound();
        }

        return View(new CustomerEditViewModel { Id = id, Name = customer.Name, Email = customer.Email, Note = customer.Note, Devices = customer.Devices });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, CustomerEditViewModel input)
    {
        var error = await customers.UpdateAsync(id, new CustomerInput(input.Name, input.Email, input.Note));
        if (error == "Customer.Error.NotFound")
        {
            return NotFound();
        }

        if (error is not null)
        {
            ModelState.AddModelError(error == "Customer.Error.Email" ? nameof(input.Email) : nameof(input.Name), T[error]);
            input.Id = id;
            input.Devices = (await customers.GetAsync(id))?.Devices ?? [];
            return View(input);
        }

        TempData["Success"] = T["Customer.Saved"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var error = await customers.DeleteAsync(id);
        TempData[error is null ? "Success" : "Error"] = T[error ?? "Customer.Deleted"].Value;
        return RedirectToAction(nameof(Index));
    }
}
