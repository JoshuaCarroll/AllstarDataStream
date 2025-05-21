using Microsoft.AspNetCore.Mvc;


namespace AsteriskAMIStream.Controllers
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
