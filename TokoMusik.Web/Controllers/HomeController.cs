
using Microsoft.AspNetCore.Mvc;

namespace TokoMusik.Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
