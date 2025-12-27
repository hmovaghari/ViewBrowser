using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ViewBrowser.Models;
using ViewBrowser.Services;

namespace ViewBrowser.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IVpnService _vpnService;

        public HomeController(ILogger<HomeController> logger, IVpnService vpnService)
        {
            _logger = logger;
            _vpnService = vpnService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult ProxyBrowser(string? url)
        {
            ViewBag.InitialUrl = url ?? "";
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
