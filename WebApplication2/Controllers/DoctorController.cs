using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace WebApplication2.Controllers
{
    public class DoctorController : Controller
    {
        // This action renders your main patient portal dashboard
        public IActionResult DoctorDashboard()
        {
            // Hardcoded alert data structured exactly like your UI design mockup
            var mockAlerts = new List<dynamic>
            {
                new {
                    Surname = "Dlamini", FirstName = "Sipho", IdNumber = "8706125432089",
                    TestName = "Hb", ResultValue = "5.2", Unit = "g/dL", RefRange = "ref 13.5–17.5",
                    Category = "FBC", Date = "06 Apr 2026", AlertLevel = "Critical low"
                },
                new {
                    Surname = "Petersen", FirstName = "Riaan", IdNumber = "7901044321008",
                    TestName = "WBC", ResultValue = "42.1", Unit = "10⁹/L", RefRange = "ref 4.5–11.0",
                    Category = "FBC + Diff", Date = "07 Apr 2026", AlertLevel = "Critical high"
                },
                new {
                    Surname = "Nkosi", FirstName = "Thandiwe", IdNumber = "9203156789012",
                    TestName = "Platelets", ResultValue = "22", Unit = "10⁹/L", RefRange = "ref 150–400",
                    Category = "FBC", Date = "08 Apr 2026", AlertLevel = "Critical low"
                },
                new {
                    Surname = "Hendricks", FirstName = "Ayesha", IdNumber = "9507254012345",
                    TestName = "INR", ResultValue = "4.8", Unit = "", RefRange = "ref 0.8–1.2",
                    Category = "Coagulation", Date = "09 Apr 2026", AlertLevel = "High"
                }
            };

            return View(mockAlerts);
        }

        // Placeholder for the creation view when "+ New test request" is clicked
        public IActionResult NewTestRequest()
        {
            return View();
        }
    }
}