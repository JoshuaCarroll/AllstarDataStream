using Microsoft.AspNetCore.Mvc;


namespace AsteriskDataStream.Controllers
{
    [Route("/")]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
