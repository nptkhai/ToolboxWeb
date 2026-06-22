using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ToolboxWeb.Web.Controllers;

[Authorize]
public class ComponentsController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
