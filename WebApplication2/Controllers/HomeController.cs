using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using NMBDHL.Auth.Model;
using NMBDHL.Auth.Repository;
using NMBHLabSys.LabManagerSubsystem.Services;
using System.Diagnostics;
using System.Security.Claims;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IAuthRepository _authRepository;
        private readonly ILabManagerReportService _reports;

        public HomeController(
            ILogger<HomeController> logger,
            IAuthRepository authRepository)
        {
            _logger = logger;
            _authRepository = authRepository;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id
                    ?? HttpContext.TraceIdentifier
            });
        }

        [HttpGet]
        [HttpGet]
        public IActionResult LoginPage(string? returnUrl = null)
        {
            var login = new Login
            {
                ReturnUrl = returnUrl ?? string.Empty,
                RememberLogin = true
            };

            return View("LoginPage", login);
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            return LoginPage(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(Login login)
        {
            //if (!ModelState.IsValid)
            //{
            //    return View("LoginPage", login);
            //}

            User user;

            try
            {
                user = await _authRepository.ValidateUser(login);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login validation failed.");

                ViewBag.Message = "Unable to log in. Please try again.";
                return View("LoginPage", login);
            }

            if (user == null)
            {
                ViewBag.Message = "Invalid email or password.";
                return View("LoginPage", login);
            }

            if (!user.IsActive)
            {
                ViewBag.Message = "Your account is inactive.";
                return View("LoginPage", login);
            }

            // Normalize the role to avoid problems caused by spaces
            // or differences in letter casing in the database.
            string role = (user.Role ?? string.Empty).Trim();

            string dashboardAction;

            switch (role.ToLowerInvariant())
            {
                case "admin":
                    dashboardAction = "AdminDashboard";
                    break;

                case "patient":
                    dashboardAction = "PatientDashboard";
                    break;

                case "doctor":
                    dashboardAction = "DoctorDashboard";
                    break;

                case "lab manager":
                    dashboardAction = "Dashboard";
                    break;

                case "lab technician":
                    dashboardAction = "LabTechnicianDashboard";
                    break;

                default:
                    _logger.LogWarning(
                        "Login rejected because user {UserID} has an unknown role.",
                        user.UserID);

                    ViewBag.Message =
                        "Your account has an invalid role. Contact the administrator.";

                    return View("LoginPage", login);
            }

            var claims = new List<Claim>
    {
        new Claim(
            ClaimTypes.NameIdentifier,
            user.UserID.ToString()),

        new Claim(
            ClaimTypes.Name,
            $"{user.Name} {user.Surname}"),

        new Claim(
            ClaimTypes.Email,
            user.Email),

        new Claim(
            ClaimTypes.Role,
            role)
    };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            try
            {
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = login.RememberLogin
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not create the login cookie.");

                ViewBag.Message =
                    "Unable to complete sign-in. Please try again.";

                return View("LoginPage", login);
            }

            // Only allow local return URLs to prevent external redirects.
            if (!string.IsNullOrWhiteSpace(login.ReturnUrl) &&
                Url.IsLocalUrl(login.ReturnUrl))
            {
                return LocalRedirect(login.ReturnUrl);
            }

            return RedirectToAction(dashboardAction, "Home");
        }

        public IActionResult AdminDashboard()
        {
            return View();
        }

        public IActionResult PatientDashboard()
        {
            return View();
        }

        public IActionResult LabManagerDashboard()
        {
            return View();
        }

        public async Task<IActionResult> Dashboard()
        {
            return RedirectToAction("Dashboard", "LabManager");
        }


        public IActionResult LabTechnicianDashboard()
        {
            return View();
        }

        public IActionResult DoctorDashboard()
        {
            return View();
        }

        public IActionResult LoadDashboardSection(string section)
        {
            if (string.IsNullOrEmpty(section))
                section = "alerts";

            section = section.ToLowerInvariant();

            switch (section)
            {
                case "alerts":
                    {
                        ViewBag.TotalActiveRequests = 24;
                        ViewBag.CriticalAlertsCount = 4;
                        ViewBag.PendingCollections = 7;
                        ViewBag.TurnaroundCompliance = "94.2%";

                        var recentRequests = new List<dynamic>
                    {
                        new { RequestNumber = "REQ-2026-0042", PatientName = "Sipho Dlamini", IdNumber = "8706125432089", Tests = "Hb, Platelets", Urgency = "STAT", Status = "In Progress", TimeElapsed = "15 mins ago" },
                        new { RequestNumber = "REQ-2026-0041", PatientName = "Riaan Petersen", IdNumber = "7901044321008", Tests = "WBC, Diff Count", Urgency = "Urgent", Status = "Sample(s) Received", TimeElapsed = "45 mins ago" },
                        new { RequestNumber = "REQ-2026-0040", PatientName = "Thandiwe Nkosi", IdNumber = "9203156789012", Tests = "Peripheral Blood Film", Urgency = "Routine", Status = "Submitted", TimeElapsed = "1 hour ago" },
                        new { RequestNumber = "REQ-2026-0039", PatientName = "Ayesha Hendricks", IdNumber = "9507254012345", Tests = "INR, Coagulation", Urgency = "STAT", Status = "Completed", TimeElapsed = "2 hours ago" },
                        new { RequestNumber = "REQ-2026-0038", PatientName = "Chantel Myburgh", IdNumber = "8811235098081", Tests = "FBC Panel", Urgency = "Routine", Status = "Released by doctor", TimeElapsed = "3 hours ago" }
                    };

                        ViewBag.RecentRequests = recentRequests;

                        var mockAlerts = new List<dynamic>
                    {
                        new { Surname = "Dlamini", FirstName = "Sipho", IdNumber = "8706125432089", TestName = "Hb (Hemoglobin)", ResultValue = "5.2", Unit = "g/dL", RefRange = "ref 13.5–17.5", Category = "Full Blood Count", Date = "Today, 11:42", AlertLevel = "Critical Low" },
                        new { Surname = "Petersen", FirstName = "Riaan", IdNumber = "7901044321008", TestName = "WBC (White Blood Cell)", ResultValue = "42.1", Unit = "10⁹/L", RefRange = "ref 4.5–11.0", Category = "Differential Count", Date = "Today, 11:15", AlertLevel = "Critical High" },
                        new { Surname = "Nkosi", FirstName = "Thandiwe", IdNumber = "9203156789012", TestName = "Platelets", ResultValue = "22", Unit = "10⁹/L", RefRange = "ref 150–400", Category = "Full Blood Count", Date = "Yesterday, 15:30", AlertLevel = "Critical Low" }
                    };

                        return PartialView(
                            "~/Views/Doctor/_AbnormalAlertsPartial.cshtml",
                            mockAlerts);
                    }

                case "managepatients":
                    {
                        var mockPatients = new List<dynamic>
                    {
                        new { Surname = "Dlamini", FirstName = "Sipho", IdNumber = "8706125432089", Cellphone = "0823456789", Email = "sipho.d@mail.co.za", Conditions = "Hypertension, Type 2 Diabetes", Allergies = "Penicillin", Medication = "Metformin 500mg, Amlodipine 5mg" },
                        new { Surname = "Petersen", FirstName = "Riaan", IdNumber = "7901044321008", Cellphone = "0731234567", Email = "riaan.p@webmail.co.za", Conditions = "Hypercholesterolemia", Allergies = "Sulfa Drugs", Medication = "Atorvastatin 20mg" },
                        new { Surname = "Nkosi", FirstName = "Thandiwe", IdNumber = "9203156789012", Cellphone = "0819876543", Email = "thandi.n@health.org", Conditions = "None Chronic", Allergies = "Bee Stings, Aspirin", Medication = "None" },
                        new { Surname = "Hendricks", FirstName = "Ayesha", IdNumber = "9507254012345", Cellphone = "0645551234", Email = "ayesha.h@outlook.com", Conditions = "Asthma", Allergies = "Latex, Dust Mites", Medication = "Salbutamol Inhaler as needed" }
                    };

                        return PartialView(
                            "~/Views/Doctor/_ManagePatientsPartial.cshtml",
                            mockPatients);
                    }

                case "createrequest":
                    {
                        ViewBag.PatientsList = new List<dynamic>
                    {
                        new { IdNumber = "8706125432089", FullName = "Dlamini, Sipho" },
                        new { IdNumber = "7901044321008", FullName = "Petersen, Riaan" },
                        new { IdNumber = "9203156789012", FullName = "Nkosi, Thandiwe" },
                        new { IdNumber = "9507254012345", FullName = "Hendricks, Ayesha" }
                    };

                        ViewBag.TestPanels = new List<string>
                    {
                        "Full Blood Count (FBC)",
                        "Hemoglobin (Hb) Isolation",
                        "Differential White Cell Count",
                        "Platelet Count Analysis",
                        "Prothrombin Time & INR",
                        "Peripheral Blood Film Study"
                    };

                        return PartialView(
                            "~/Views/Doctor/_CreateRequestPartial.cshtml");
                    }

                case "trackprogress":
                    {
                        var historicalTrackingData = new List<dynamic>
                    {
                        new { RequestId = 1042, PatientName = "Sipho Dlamini", IdNumber = "8706125432089", Panels = "Hb, Platelets", DateCreated = "21 May 2026", Urgency = "STAT", Status = "In Progress" },
                        new { RequestId = 1041, PatientName = "Riaan Petersen", IdNumber = "7901044321008", Panels = "WBC, Diff Count", DateCreated = "21 May 2026", Urgency = "Urgent", Status = "Sample(s) Received" },
                        new { RequestId = 1040, PatientName = "Thandiwe Nkosi", IdNumber = "9203156789012", Panels = "Peripheral Blood Film", DateCreated = "20 May 2026", Urgency = "Routine", Status = "Submitted" },
                        new { RequestId = 1039, PatientName = "Ayesha Hendricks", IdNumber = "9507254012345", Panels = "INR, Coagulation", DateCreated = "19 May 2026", Urgency = "STAT", Status = "Completed" },
                        new { RequestId = 1038, PatientName = "Chantel Myburgh", IdNumber = "8811235098081", Panels = "FBC Panel", DateCreated = "18 May 2026", Urgency = "Routine", Status = "Released by doctor" }
                    };

                        return PartialView(
                            "~/Views/Doctor/_TrackProgressPartial.cshtml",
                            historicalTrackingData);
                    }

                case "reports":
                    {
                        ViewBag.DefaultStartDate =
                            DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");

                        ViewBag.DefaultEndDate =
                            DateTime.Now.ToString("yyyy-MM-dd");

                        return PartialView(
                            "~/Views/Doctor/_ReportsPortalPartial.cshtml");
                    }

                case "profilesettings":
                    {
                        return PartialView(
                            "~/Views/Doctor/_ProfileSettings.cshtml");
                    }

                default:
                    return Content(
                        "<div class='text-white p-4'>" +
                        "<h5>Section placeholder</h5></div>");
            }
        }
    }
}