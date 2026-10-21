using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NMBDHL.DataAccess.SQLDataBaseAccess;
using NMBDHL.Doctor.Model;
using NMBDHL.Doctor.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApplication2.Controllers
{
    public class DoctorController : Controller
    {
        private readonly IDoctorRepository _doctorRepo;

        public DoctorController(IConfiguration config)
        {
            var db = new SQLDataBaseAccess(config);
            _doctorRepo = new DoctorRepository(db);
        }

        public IActionResult DoctorDashboard()
        {
            return View("~/Views/Home/DoctorDashboard.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> LoadDashboardSection(string section)
        {
            if (string.IsNullOrEmpty(section)) section = "alerts";
            section = section.ToLower();

            int doctorId = 1;

            switch (section)
            {
                case "alerts":
                    ViewBag.TotalActiveRequests = 22;
                    ViewBag.CriticalAlertsCount = 4;
                    ViewBag.PendingCollections = 6;
                    ViewBag.TurnaroundCompliance = "94.2%";

                    try
                    {
                        var dbRecent = await _doctorRepo.GetRecentRequestsAsync(doctorId);
                        var dbAlerts = await _doctorRepo.GetAbnormalAlertsAsync(doctorId);

                        if (dbAlerts != null && dbAlerts.Any() && dbRecent != null && dbRecent.Any())
                        {
                            ViewBag.RecentRequests = dbRecent;
                            return PartialView("~/Views/Doctor/_AbnormalAlertsPartial.cshtml", dbAlerts);
                        }
                    }
                    catch { }

                    ViewBag.RecentRequests = new List<dynamic>
                    {
                        new { RequestNumber = "REQ-2026-0042", PatientName = "Sipho Dlamini", IdNumber = "8706125432089", Tests = "Hb, Platelets", Urgency = "STAT", Status = "In Progress", TimeElapsed = "15 mins ago" },
                        new { RequestNumber = "REQ-2026-0041", PatientName = "Riaan Petersen", IdNumber = "7901044321008", Tests = "WBC, Diff Count", Urgency = "Urgent", Status = "Samples Received", TimeElapsed = "45 mins ago" },
                        new { RequestNumber = "REQ-2026-0040", PatientName = "Thandiwe Nkosi", IdNumber = "9203156789012", Tests = "Peripheral Blood Film", Urgency = "Routine", Status = "Submitted", TimeElapsed = "1 hour ago" },
                        new { RequestNumber = "REQ-2026-0039", PatientName = "Ayesha Hendricks", IdNumber = "9507254012345", Tests = "INR, Coagulation", Urgency = "STAT", Status = "Completed", TimeElapsed = "2 hours ago" },
                        new { RequestNumber = "REQ-2026-0038", PatientName = "Chantel Myburgh", IdNumber = "8811235098081", Tests = "FBC Panel", Urgency = "Routine", Status = "Released", TimeElapsed = "3 hours ago" }
                    };

                    var fallbackAlerts = new List<dynamic>
                    {
                        new { Surname = "Dlamini", FirstName = "Sipho", IdNumber = "8706125432089", TestName = "Hb (Hemoglobin)", ResultValue = "5.2", Unit = "g/dL", RefRange = "ref 13.5–17.5", Category = "Full Blood Count", Date = "Today, 11:42", AlertLevel = "Critical Low" },
                        new { Surname = "Petersen", FirstName = "Riaan", IdNumber = "7901044321008", TestName = "WBC (White Blood Cell)", ResultValue = "42.1", Unit = "10⁹/L", RefRange = "ref 4.5–11.0", Category = "Differential Count", Date = "Today, 11:15", AlertLevel = "Critical High" },
                        new { Surname = "Nkosi", FirstName = "Thandiwe", IdNumber = "9203156789012", TestName = "Platelets", ResultValue = "22", Unit = "10⁹/L", RefRange = "ref 150–400", Category = "Full Blood Count", Date = "Yesterday, 15:30", AlertLevel = "Critical Low" }
                    };

                    return PartialView("~/Views/Doctor/_AbnormalAlertsPartial.cshtml", fallbackAlerts);

                case "managepatients":
                    try
                    {
                        var dbPatients = await _doctorRepo.GetManagePatientsAsync(doctorId);
                        if (dbPatients != null && dbPatients.Any())
                        {
                            return PartialView("~/Views/Doctor/_ManagePatientsPartial.cshtml", dbPatients.Select(x => (dynamic)x).ToList());
                        }
                    }
                    catch { }

                    var fallbackPatients = new List<dynamic>
                    {
                        new { Surname = "Dlamini", FirstName = "Sipho", IdNumber = "8706125432089", Cellphone = "0823456789", Email = "sipho.d@mail.co.za", Conditions = "Hypertension, Type 2 Diabetes", Allergies = "Penicillin", Medication = "Metformin 500mg, Amlodipine 5mg" },
                        new { Surname = "Petersen", FirstName = "Riaan", IdNumber = "7901044321008", Cellphone = "0731234567", Email = "riaan.p@webmail.co.za", Conditions = "Hypercholesterolemia", Allergies = "Sulfa Drugs", Medication = "Atorvastatin 20mg" },
                        new { Surname = "Nkosi", FirstName = "Thandiwe", IdNumber = "9203156789012", Cellphone = "0819876543", Email = "thandi.n@health.org", Conditions = "None Chronic", Allergies = "Bee Stings, Aspirin", Medication = "None" },
                        new { Surname = "Hendricks", FirstName = "Ayesha", IdNumber = "9507254012345", Cellphone = "0645551234", Email = "ayesha.h@outlook.com", Conditions = "Asthma", Allergies = "Latex, Dust Mites", Medication = "Salbutamol Inhaler as needed" }
                    };

                    return PartialView("~/Views/Doctor/_ManagePatientsPartial.cshtml", fallbackPatients);

                case "createrequest":
                    try
                    {
                        var dbDropdown = await _doctorRepo.GetPatientsDropdownAsync();
                        var dbPanels = await _doctorRepo.GetTestPanelsAsync();
                        if (dbDropdown != null && dbDropdown.Any() && dbPanels != null && dbPanels.Any())
                        {
                            ViewBag.PatientsList = dbDropdown.Select(x => (dynamic)x).ToList();
                            ViewBag.TestPanels = dbPanels.ToList();
                            return PartialView("~/Views/Doctor/_CreateRequestPartial.cshtml");
                        }
                    }
                    catch { }

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

                    return PartialView("~/Views/Doctor/_CreateRequestPartial.cshtml");

                case "trackprogress":
                    try
                    {
                        var dbTrack = await _doctorRepo.GetTrackProgressAsync(doctorId);
                        if (dbTrack != null && dbTrack.Any())
                        {
                            return PartialView("~/Views/Doctor/_TrackProgressPartial.cshtml", dbTrack.Select(x => (dynamic)x).ToList());
                        }
                    }
                    catch { }

                    var fallbackTrack = new List<dynamic>
                    {
                        new { RequestId = 1042, PatientName = "Sipho Dlamini", IdNumber = "8706125432089", Panels = "Hb, Platelets", DateCreated = "21 May 2026", Urgency = "STAT", Status = "In Progress" },
                        new { RequestId = 1041, PatientName = "Riaan Petersen", IdNumber = "7901044321008", Panels = "WBC, Diff Count", DateCreated = "21 May 2026", Urgency = "Urgent", Status = "Samples Received" },
                        new { RequestId = 1040, PatientName = "Thandiwe Nkosi", IdNumber = "9203156789012", Panels = "Peripheral Blood Film", DateCreated = "20 May 2026", Urgency = "Routine", Status = "Submitted" },
                        new { RequestId = 1039, PatientName = "Ayesha Hendricks", IdNumber = "9507254012345", Panels = "INR, Coagulation", DateCreated = "19 May 2026", Urgency = "STAT", Status = "Completed" },
                        new { RequestId = 1038, PatientName = "Chantel Myburgh", IdNumber = "8811235098081", Panels = "FBC Panel", DateCreated = "18 May 2026", Urgency = "Routine", Status = "Released" }
                    };

                    return PartialView("~/Views/Doctor/_TrackProgressPartial.cshtml", fallbackTrack);

                case "reports":
                    ViewBag.DefaultStartDate = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
                    ViewBag.DefaultEndDate = DateTime.Now.ToString("yyyy-MM-dd");
                    return PartialView("~/Views/Doctor/_ReportsPortalPartial.cshtml");

                case "profilesettings":
                    return PartialView("~/Views/Doctor/_ProfileSettingsPartial.cshtml");

                default:
                    return PartialView("~/Views/Doctor/_AbnormalAlertsPartial.cshtml");
            }
        }
    }
}