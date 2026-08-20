using Microsoft.AspNetCore.Mvc;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    public class LabTechnicianController : Controller
    {
        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult StatUrgent()
        {
            return View();
        }

        public IActionResult VerifyResults()
        {
            return View();
        }

        public IActionResult TestDetail()
        {
            return View();
        }

        public IActionResult Escalations()
        {
            return View();
        }

        public IActionResult Reports()
        {
            return View();
        }

        public IActionResult Profile()
        {
            return View();
        }

        public IActionResult EditProfile()
        {
            return View();
        }

        public IActionResult ChangePassword()
        {
            return View();
        }

        public IActionResult AwaitingSelection()
        {
            return View();
        }

        public IActionResult AssignedTests()
        {
            return View();
        }

        [HttpGet]
        public IActionResult CaptureResults()
        {
            return View(new CaptureResultsViewModel());
        }

        [HttpPost]
        public IActionResult CaptureResults(CaptureResultsViewModel model)
        {
            model.HaemoglobinStatus =
                EvaluateResult(model.Haemoglobin, 12.0m, 16.0m);

            model.WccStatus =
                EvaluateResult(model.Wcc, 4.0m, 11.0m);

            model.PlateletsStatus =
                EvaluateResult(model.Platelets, 150m, 450m);

            return View(model);
        }

        private string EvaluateResult(
            string value,
            decimal lowerLimit,
            decimal upperLimit)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Awaiting Result";
            }

            if (!decimal.TryParse(value, out decimal result))
            {
                return "Invalid Result";
            }

            if (result < lowerLimit)
            {
                return "Abnormally Low";
            }

            if (result > upperLimit)
            {
                return "Abnormally High";
            }

            return "Within Normal Range";
        }

        public IActionResult VerificationQueue()
        {
            return View();
        }

        public IActionResult ReviewQueue()
        {
            return View();
        }

        public IActionResult ReceiveSamples()
        {
            return View();
        }
    }
}