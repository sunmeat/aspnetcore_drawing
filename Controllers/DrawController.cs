using Microsoft.AspNetCore.Mvc;

namespace CollaborativeDrawingBoard.Controllers
{
    public class DrawController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Login()
        {
            return View();
        }
    }
}
