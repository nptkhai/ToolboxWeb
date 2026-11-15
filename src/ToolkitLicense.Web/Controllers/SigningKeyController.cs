using Microsoft.AspNetCore.Mvc;
using ToolkitLicense.Web.Infrastructure;
using ToolkitLicense.Web.ViewModels;

namespace ToolkitLicense.Web.Controllers;

/// <summary>The public half of the license signing key, to build into the desktop app (M4).</summary>
public sealed class SigningKeyController(SigningKey key, LicenseStorage storage) : Controller
{
    public IActionResult Index() => View(new SigningKeyViewModel
    {
        PublicKeyPem = key.PublicKeyPem,
        PublicKeyBase64 = key.PublicKeyBase64,
        Fingerprint = key.Fingerprint,
        KeyFile = storage.SigningKeyPath,
        DataRoot = storage.DataRoot
    });
}
