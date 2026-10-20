using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Microsoft.Data.SqlClient;
using WebApplication2.Models;
using WebApplication2.Services;

namespace WebApplication2.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly LabTechnicianDataService _labTechnicianData;
        private readonly IConfiguration _configuration;

        public HomeController(
            ILogger<HomeController> logger,
            LabTechnicianDataService labTechnicianData,
            IConfiguration configuration)
        {
            _logger = logger;
            _labTechnicianData = labTechnicianData;
            _configuration = configuration;
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
        [HttpPost]
        public IActionResult Login(string Username)
        {
            var user = Username?.Trim().ToLower();

            if (user == "admin")
                return RedirectToAction("AdminDashboard");

            if (user == "laboratory manager")
                return RedirectToAction("Dashboard", "LabManager");

            if (user == "doctor")
                return RedirectToAction("DoctorDashboard");

            if (user == "lab technician")
                return RedirectToAction("LabTechnicianDashboard");

            if (user == "patient")
                return RedirectToAction("PatientDashboard");

            return View();
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
        public async Task<IActionResult> LabTechnicianDashboard(
            CancellationToken cancellationToken)
        {
            try
            {
                var connectionString =
                    _configuration.GetConnectionString("DefaultConnection");

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException(
                        "The DefaultConnection connection string is missing.");
                }

                // Dashboard-only query: leaves the other subsystem actions untouched.
                const string statsSql = """
                    SELECT
                        (
                            SELECT COUNT(*)
                            FROM dbo.SampleReceipt sr
                            WHERE NOT EXISTS
                            (
                                SELECT 1
                                FROM dbo.TestResult tres
                                WHERE tres.TestRequestID = sr.TestRequestID
                            )
                        ) AS AwaitingSelection,
                        (
                            SELECT COUNT(*)
                            FROM dbo.TestResult tres
                            OUTER APPLY
                            (
                                SELECT TOP (1) rv.Decision
                                FROM dbo.ResultVerification rv
                                WHERE rv.TestResultID = tres.TestResultID
                                ORDER BY rv.VerificationDateTime DESC,
                                         rv.ResultVerificationID DESC
                            ) latest
                            WHERE latest.Decision IS NULL
                               OR UPPER(LTRIM(RTRIM(latest.Decision))) NOT IN
                                  ('APPROVED', 'RETURNED', 'RETURN', 'REJECTED')
                        ) AS AwaitingVerification,
                        (
                            SELECT COUNT(*)
                            FROM dbo.TestResult
                            WHERE CompletionDateTime >= CONVERT(date, GETDATE())
                              AND CompletionDateTime < DATEADD(day, 1, CONVERT(date, GETDATE()))
                        ) AS CompletedToday,
                        (
                            SELECT COUNT(*)
                            FROM dbo.TestRequest tr
                            WHERE UPPER(LTRIM(RTRIM(COALESCE(tr.Urgency, ''))))
                                  IN ('STAT', 'URGENT')
                              AND COALESCE(tr.Status, '') NOT IN
                                  ('Cancelled', 'Canceled', 'Completed', 'Rejected')
                        ) AS UrgentCount,
                        (
                            SELECT COUNT(*)
                            FROM dbo.TestResult tres
                            INNER JOIN dbo.TestType tt ON tt.TestTypeID = tres.TestTypeID
                            INNER JOIN dbo.SampleReceipt sr ON sr.TestRequestID = tres.TestRequestID
                            WHERE sr.DateTimeReceived IS NOT NULL
                              AND COALESCE(tt.TurnAroundMinutes, 0) > 0
                              AND DATEADD(MINUTE, tt.TurnAroundMinutes, sr.DateTimeReceived) < GETDATE()
                              AND NOT EXISTS
                              (
                                  SELECT 1
                                  FROM dbo.ResultVerification rv
                                  WHERE rv.TestResultID = tres.TestResultID
                                    AND UPPER(LTRIM(RTRIM(COALESCE(rv.Decision, '')))) = 'APPROVED'
                              )
                        ) AS OverdueCount;
                    """;

                await using var connection = new SqlConnection(connectionString);

                var stats = await connection.QuerySingleAsync<LabTechnicianDashboardStats>(
                    new CommandDefinition(
                        statsSql,
                        cancellationToken: cancellationToken));

                var receivedSamples = await _labTechnicianData
                    .GetReceivedSamplesAsync(cancellationToken);

                var assignedTests = await _labTechnicianData
                    .GetAssignedTestsAsync(cancellationToken);

                var incompleteAssignedTests = assignedTests
                    .Where(test => !_labTechnicianData.IsTestCompleted(
                        test.SampleReceiptID, test.TestTypeID))
                    .ToList();

                var assignedSampleIds = new HashSet<int>(
                    assignedTests.Select(test => test.SampleReceiptID));

                // A received sample is awaiting selection when no test has yet
                // been assigned to that sample in the current service session.
                stats.AwaitingSelection = receivedSamples.Count(sample =>
                    !sample.IsRejected && !assignedSampleIds.Contains(sample.SampleReceiptID));

                var selectedCount = assignedTests.Count;
                var verifiedCount = await connection.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        """
                        SELECT COUNT(*)
                        FROM dbo.TestResult tres
                        OUTER APPLY
                        (
                            SELECT TOP (1) rv.Decision
                            FROM dbo.ResultVerification rv
                            WHERE rv.TestResultID = tres.TestResultID
                            ORDER BY rv.VerificationDateTime DESC,
                                     rv.ResultVerificationID DESC
                        ) latest
                        WHERE UPPER(LTRIM(RTRIM(COALESCE(latest.Decision, '')))) = 'APPROVED';
                        """,
                        cancellationToken: cancellationToken));

                var priorityRequests = receivedSamples
                    .Where(sample => !sample.IsRejected &&
                        (string.Equals(sample.Urgency, "STAT", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(sample.Urgency, "URGENT", StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(sample => string.Equals(sample.Urgency, "STAT", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ToList();

                var now = DateTime.Now;
                var turnaroundWarnings = incompleteAssignedTests
                    .Where(test =>
                        test.SelectedAt.HasValue &&
                        test.TurnAroundMinutes > 0 &&
                        test.SelectedAt.Value.AddMinutes(test.TurnAroundMinutes) <= now.AddMinutes(30))
                    .OrderBy(test =>
                        test.SelectedAt!.Value.AddMinutes(test.TurnAroundMinutes))
                    .ToList();

                stats.AssignedTests = incompleteAssignedTests.Count;
                stats.NearingTatCount = turnaroundWarnings.Count;
                stats.SelectedCount = selectedCount;
                stats.VerifiedCount = verifiedCount;

                ViewBag.AssignedTests = stats.AssignedTests;
                ViewBag.AwaitingSelection = stats.AwaitingSelection;
                ViewBag.AwaitingVerification = stats.AwaitingVerification;
                ViewBag.CompletedToday = stats.CompletedToday;
                ViewBag.UrgentCount = stats.UrgentCount;
                ViewBag.OverdueCount = stats.OverdueCount;
                ViewBag.NearingTatCount = stats.NearingTatCount;
                ViewBag.PriorityRequests = priorityRequests;
                ViewBag.TurnaroundWarnings = turnaroundWarnings;
                ViewBag.SelectedCount = stats.SelectedCount;
                ViewBag.VerifiedCount = stats.VerifiedCount;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lab Technician dashboard failed to load.");

                ViewBag.DashboardError =
                    "Some dashboard statistics could not be loaded. Check the application logs.";

                ViewBag.AssignedTests = 0;
                ViewBag.AwaitingSelection = 0;
                ViewBag.AwaitingVerification = 0;
                ViewBag.CompletedToday = 0;
                ViewBag.UrgentCount = 0;
                ViewBag.OverdueCount = 0;
                ViewBag.NearingTatCount = 0;
                ViewBag.PriorityRequests = new List<LabTechnicianQueueItem>();
                ViewBag.TurnaroundWarnings = new List<LabTechnicianQueueItem>();
                ViewBag.SelectedCount = 0;
                ViewBag.VerifiedCount = 0;

                return View();
            }
        }

        private sealed class LabTechnicianDashboardStats
        {
            public int AssignedTests { get; set; }
            public int AwaitingSelection { get; set; }
            public int AwaitingVerification { get; set; }
            public int CompletedToday { get; set; }
            public int UrgentCount { get; set; }
            public int OverdueCount { get; set; }
            public int NearingTatCount { get; set; }
            public int SelectedCount { get; set; }
            public int VerifiedCount { get; set; }
        }
        public IActionResult DoctorDashboard()
        {
            return View();
        }

        public IActionResult LoadDashboardSection(string section)
        {
            if (string.IsNullOrEmpty(section)) section = "alerts";
            section = section.ToLower();

            switch (section)
            {
                // REQ 5: View Alerts (Comprehensive Dashboard with Tables, Lists, and Charts)
                case "alerts":
                    // 1. Top Bar Metric KPIs
                    ViewBag.TotalActiveRequests = 24;
                    ViewBag.CriticalAlertsCount = 4;
                    ViewBag.PendingCollections = 7;
                    ViewBag.TurnaroundCompliance = "94.2%";

                    // 2. Recent Test Request Datatable Rows (REQ 3 Workflow statuses)
                    var recentRequests = new List<dynamic>
                    {
                        new { RequestNumber = "REQ-2026-0042", PatientName = "Sipho Dlamini", IdNumber = "8706125432089", Tests = "Hb, Platelets", Urgency = "STAT", Status = "In Progress", TimeElapsed = "15 mins ago" },
                        new { RequestNumber = "REQ-2026-0041", PatientName = "Riaan Petersen", IdNumber = "7901044321008", Tests = "WBC, Diff Count", Urgency = "Urgent", Status = "Sample(s) Received", TimeElapsed = "45 mins ago" },
                        new { RequestNumber = "REQ-2026-0040", PatientName = "Thandiwe Nkosi", IdNumber = "9203156789012", Tests = "Peripheral Blood Film", Urgency = "Routine", Status = "Submitted", TimeElapsed = "1 hour ago" },
                        new { RequestNumber = "REQ-2026-0039", PatientName = "Ayesha Hendricks", IdNumber = "9507254012345", Tests = "INR, Coagulation", Urgency = "STAT", Status = "Completed", TimeElapsed = "2 hours ago" },
                        new { RequestNumber = "REQ-2026-0038", PatientName = "Chantel Myburgh", IdNumber = "8811235098081", Tests = "FBC Panel", Urgency = "Routine", Status = "Released by doctor", TimeElapsed = "3 hours ago" }
                    };
                    ViewBag.RecentRequests = recentRequests;

                    // 3. Abnormal Critical Alerts Feed Array (REQ 5)
                    var mockAlerts = new List<dynamic>
                    {
                        new { Surname = "Dlamini", FirstName = "Sipho", IdNumber = "8706125432089", TestName = "Hb (Hemoglobin)", ResultValue = "5.2", Unit = "g/dL", RefRange = "ref 13.5–17.5", Category = "Full Blood Count", Date = "Today, 11:42", AlertLevel = "Critical Low" },
                        new { Surname = "Petersen", FirstName = "Riaan", IdNumber = "7901044321008", TestName = "WBC (White Blood Cell)", ResultValue = "42.1", Unit = "10⁹/L", RefRange = "ref 4.5–11.0", Category = "Differential Count", Date = "Today, 11:15", AlertLevel = "Critical High" },
                        new { Surname = "Nkosi", FirstName = "Thandiwe", IdNumber = "9203156789012", TestName = "Platelets", ResultValue = "22", Unit = "10⁹/L", RefRange = "ref 150–400", Category = "Full Blood Count", Date = "Yesterday, 15:30", AlertLevel = "Critical Low" }
                    };

                    return PartialView("~/Views/Doctor/_AbnormalAlertsPartial.cshtml", mockAlerts);

                // REQ 1: Manage Patient Records Portal
                case "managepatients":
                    var mockPatients = new List<dynamic>
                    {
                        new { Surname = "Dlamini", FirstName = "Sipho", IdNumber = "8706125432089", Cellphone = "0823456789", Email = "sipho.d@mail.co.za", Conditions = "Hypertension, Type 2 Diabetes", Allergies = "Penicillin", Medication = "Metformin 500mg, Amlodipine 5mg" },
                        new { Surname = "Petersen", FirstName = "Riaan", IdNumber = "7901044321008", Cellphone = "0731234567", Email = "riaan.p@webmail.co.za", Conditions = "Hypercholesterolemia", Allergies = "Sulfa Drugs", Medication = "Atorvastatin 20mg" },
                        new { Surname = "Nkosi", FirstName = "Thandiwe", IdNumber = "9203156789012", Cellphone = "0819876543", Email = "thandi.n@health.org", Conditions = "None Chronic", Allergies = "Bee Stings, Aspirin", Medication = "None" },
                        new { Surname = "Hendricks", FirstName = "Ayesha", IdNumber = "9507254012345", Cellphone = "0645551234", Email = "ayesha.h@outlook.com", Conditions = "Asthma", Allergies = "Latex, Dust Mites", Medication = "Salbutamol Inhaler as needed" }
                    };

                    return PartialView("~/Views/Doctor/_ManagePatientsPartial.cshtml", mockPatients);

                // REQ 2: Create Test Requests Form 
                case "createrequest":
                    // Mocking an existing patient master record set to populate our dropdown menu selection safely
                    ViewBag.PatientsList = new List<dynamic>
                    {
                        new { IdNumber = "8706125432089", FullName = "Dlamini, Sipho" },
                        new { IdNumber = "7901044321008", FullName = "Petersen, Riaan" },
                        new { IdNumber = "9203156789012", FullName = "Nkosi, Thandiwe" },
                        new { IdNumber = "9507254012345", FullName = "Hendricks, Ayesha" }
                    };

                    // Available test type panels configurations
                    ViewBag.TestPanels = new List<string> {
                        "Full Blood Count (FBC)",
                        "Hemoglobin (Hb) Isolation",
                        "Differential White Cell Count",
                        "Platelet Count Analysis",
                        "Prothrombin Time & INR",
                        "Peripheral Blood Film Study"
                    };

                    return PartialView("~/Views/Doctor/_CreateRequestPartial.cshtml");

                // REQ 3 & 4: Full Historical Track & View Results List
                case "trackprogress":
                    var historicalTrackingData = new List<dynamic>
                    {
                        new { RequestId = 1042, PatientName = "Sipho Dlamini", IdNumber = "8706125432089", Panels = "Hb, Platelets", DateCreated = "21 May 2026", Urgency = "STAT", Status = "In Progress" },
                        new { RequestId = 1041, PatientName = "Riaan Petersen", IdNumber = "7901044321008", Panels = "WBC, Diff Count", DateCreated = "21 May 2026", Urgency = "Urgent", Status = "Sample(s) Received" },
                        new { RequestId = 1040, PatientName = "Thandiwe Nkosi", IdNumber = "9203156789012", Panels = "Peripheral Blood Film", DateCreated = "20 May 2026", Urgency = "Routine", Status = "Submitted" },
                        new { RequestId = 1039, PatientName = "Ayesha Hendricks", IdNumber = "9507254012345", Panels = "INR, Coagulation", DateCreated = "19 May 2026", Urgency = "STAT", Status = "Completed" },
                        new { RequestId = 1038, PatientName = "Chantel Myburgh", IdNumber = "8811235098081", Panels = "FBC Panel", DateCreated = "18 May 2026", Urgency = "Routine", Status = "Released by doctor" }
                    };

                    return PartialView("~/Views/Doctor/_TrackProgressPartial.cshtml", historicalTrackingData);

                // REQ 6: Doctor Reports Summary Portal
                case "reports":
                    // Setting up a default date range (e.g., last 30 days) to make the form helpful
                    ViewBag.DefaultStartDate = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
                    ViewBag.DefaultEndDate = DateTime.Now.ToString("yyyy-MM-dd");

                    return PartialView("~/Views/Doctor/_ReportsPortalPartial.cshtml");

                case "profilesettings":
                    return PartialView("~/Views/Doctor/_ProfileSettings.cshtml"); // Or _ProfileSettingsPartial.cshtml if you renamed it

                default:
                    return Content("<div class='text-white p-4'><h5>Section placeholder</h5></div>");
            }
        }

    }
}