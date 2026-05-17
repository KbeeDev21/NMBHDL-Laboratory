using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult LoginPage()
        {
            return View();
        }

        public IActionResult Login()
        {
            return View();
        }

        public IActionResult AdminDashboard()
        {
            return View();
        }
        public IActionResult UserDashboard()
        {
            return View();
        }
        public IActionResult LabManagerDashboard()
        {
            return View();
        }
        public IActionResult LabTechnicianDashboard()
        {
            return View();
        }
        public IActionResult PatientDashboard()
        {
            return View();
        }
    }
}
