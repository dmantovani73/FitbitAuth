﻿using Microsoft.AspNetCore.Mvc;

namespace FitbitAuth.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet("~/")]
        public IActionResult Index() => View();
    }
}
