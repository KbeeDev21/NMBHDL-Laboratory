using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using WebApplication2.Models;
using WebApplication2.Services;

namespace WebApplication2.Controllers
{
    public class LabTechnicianController : Controller
    {
        private readonly LabTechnicianDataService _labTechnicianData;
        private readonly IConfiguration _configuration;

        public LabTechnicianController(
            LabTechnicianDataService labTechnicianData,
            IConfiguration configuration)
        {
            _labTechnicianData = labTechnicianData;
            _configuration = configuration;
        }

        // Dashboard

        [HttpGet]
        public async Task<IActionResult> Dashboard(
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

                const string statsSql = """
                    SELECT
                        (
                            SELECT COUNT(*)
                            FROM dbo.TestRequest tr
                            WHERE COALESCE(tr.Status, '') NOT IN
                                ('Cancelled', 'Canceled', 'Completed',
                                 'Awaiting Selection')
                              AND NOT EXISTS
                              (
                                  SELECT 1
                                  FROM dbo.SampleReceipt sr
                                  WHERE sr.TestRequestID = tr.TestRequestID
                              )
                        ) AS RequestsAwaitingReceipt,
                        (
                            SELECT COUNT(*)
                            FROM dbo.TestRequest tr
                            WHERE UPPER(LTRIM(RTRIM(
                                COALESCE(tr.Urgency, '')))) IN ('STAT', 'URGENT')
                              AND COALESCE(tr.Status, '') NOT IN
                                ('Cancelled', 'Canceled', 'Completed')
                        ) AS UrgentRequests,
                        (
                            SELECT COUNT(*)
                            FROM dbo.TestResult
                            WHERE CompletionDateTime >= CONVERT(date, GETDATE())
                              AND CompletionDateTime <
                                  DATEADD(day, 1, CONVERT(date, GETDATE()))
                        ) AS ResultsCompletedToday,
                        (
                            SELECT COUNT(*)
                            FROM dbo.TestResult tr
                            WHERE NOT EXISTS
                            (
                                SELECT 1
                                FROM dbo.ResultVerification rv
                                WHERE rv.TestResultID = tr.TestResultID
                                  AND UPPER(LTRIM(RTRIM(
                                      COALESCE(rv.Decision, '')))) = 'APPROVED'
                            )
                        ) AS AwaitingVerification;
                    """;

                const string urgentSql = """
                    SELECT TOP (6)
                        tr.TestRequestID,
                        COALESCE(tr.barcode, '') AS Barcode,
                        COALESCE(tr.Type, '') AS TestType,
                        COALESCE(tr.Urgency, '') AS Urgency,
                        COALESCE(tr.Status, '') AS Status,
                        tr.RequestDate,
                        LTRIM(RTRIM(CONCAT(u.Surname, ', ', u.Name)))
                            AS PatientName
                    FROM dbo.TestRequest tr
                    INNER JOIN dbo.Patient p
                        ON p.PatientID = tr.PatientID
                    INNER JOIN dbo.[User] u
                        ON u.UserID = p.UserID
                    WHERE UPPER(LTRIM(RTRIM(
                        COALESCE(tr.Urgency, '')))) IN ('STAT', 'URGENT')
                      AND COALESCE(tr.Status, '') NOT IN
                        ('Cancelled', 'Canceled', 'Completed')
                    ORDER BY
                        CASE
                            WHEN UPPER(LTRIM(RTRIM(
                                COALESCE(tr.Urgency, '')))) = 'STAT'
                            THEN 0 ELSE 1
                        END,
                        tr.RequestDate ASC;
                    """;

                await using var connection =
                    new SqlConnection(connectionString);

                await connection.OpenAsync(cancellationToken);

                var stats = await connection.QuerySingleAsync<DashboardStats>(
                    new CommandDefinition(
                        statsSql,
                        cancellationToken: cancellationToken));

                var urgentRequests =
                    (await connection.QueryAsync<DashboardUrgentItem>(
                        new CommandDefinition(
                            urgentSql,
                            cancellationToken: cancellationToken)))
                    .ToList();

                var assignedTests = await _labTechnicianData
                    .GetAssignedTestsAsync(cancellationToken);

                var assignedTestsCount = assignedTests.Count(test =>
                    !_labTechnicianData.IsTestCompleted(
                        test.SampleReceiptID,
                        test.TestTypeID));

                ViewBag.Stats = stats;
                ViewBag.AssignedTestsCount = assignedTestsCount;
                ViewBag.RequestsAwaitingReceipt = stats.RequestsAwaitingReceipt;
                ViewBag.UrgentRequestsCount = stats.UrgentRequests;
                ViewBag.ResultsCompletedToday = stats.ResultsCompletedToday;
                ViewBag.AwaitingVerification = stats.AwaitingVerification;
                ViewBag.urgentRequests = urgentRequests;
                ViewBag.UrgentRequests = urgentRequests;

                return View();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Dashboard failed: {ex}");

                TempData["ErrorMessage"] =
                    "Could not load dashboard statistics. Check the application logs.";

                ViewBag.Stats = new DashboardStats();
                ViewBag.AssignedTestsCount = 0;
                ViewBag.RequestsAwaitingReceipt = 0;
                ViewBag.UrgentRequestsCount = 0;
                ViewBag.ResultsCompletedToday = 0;
                ViewBag.AwaitingVerification = 0;
                ViewBag.urgentRequests = new List<DashboardUrgentItem>();
                ViewBag.UrgentRequests = new List<DashboardUrgentItem>();

                return View();
            }
        }

        private sealed class DashboardStats
        {
            public int RequestsAwaitingReceipt { get; set; }
            public int UrgentRequests { get; set; }
            public int ResultsCompletedToday { get; set; }
            public int AwaitingVerification { get; set; }
        }

        private sealed class DashboardUrgentItem
        {
            public int TestRequestID { get; set; }
            public string Barcode { get; set; } = string.Empty;
            public string TestType { get; set; } = string.Empty;
            public string Urgency { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public DateTime? RequestDate { get; set; }
            public string PatientName { get; set; } = string.Empty;
        }

        [HttpGet]
        public IActionResult StatUrgent() => View();

        [HttpGet]
        public IActionResult TestDetail() => View();

        [HttpGet]
        public IActionResult Escalations() => View();

        [HttpGet]
        public IActionResult Reports() => View();

        [HttpGet]
        public IActionResult Profile() => View();

        [HttpGet]
        public IActionResult EditProfile() => View();

        [HttpGet]
        public IActionResult ChangePassword() => View();

        // Sample receipt and test selection

        [HttpGet]
        public async Task<IActionResult> AwaitingSelection(
            CancellationToken cancellationToken)
        {
            try
            {
                var samples = await _labTechnicianData
                    .GetReceivedSamplesAsync(cancellationToken);

                ViewBag.TestTypes = await _labTechnicianData
                    .GetAvailableTestTypesAsync(cancellationToken);

                ViewBag.Consumables = await _labTechnicianData
                    .GetConsumableStockAsync(cancellationToken);

                return View(samples.Where(x => !x.IsRejected).ToList());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"AwaitingSelection failed: {ex}");

                TempData["ErrorMessage"] =
                    "Could not load Awaiting Selection data. Check the application logs.";

                ViewBag.TestTypes = new List<LabTechnicianQueueItem>();
                ViewBag.Consumables = new List<ConsumableStockItem>();

                return View(new List<LabTechnicianQueueItem>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectTest(
            int sampleReceiptId,
            int testTypeId,
            CancellationToken cancellationToken)
        {
            if (sampleReceiptId <= 0 || testTypeId <= 0)
            {
                TempData["ErrorMessage"] = "Select a valid sample and test.";
                return RedirectToAction(nameof(AwaitingSelection));
            }

            try
            {
                var result = await _labTechnicianData.SelectTestAsync(
                    sampleReceiptId,
                    testTypeId,
                    cancellationToken);

                TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] =
                    result.Message;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"SelectTest failed: {ex}");

                TempData["ErrorMessage"] =
                    "The test could not be selected. Check the application logs.";
            }

            return RedirectToAction(nameof(AwaitingSelection));
        }

        // Assigned tests and result capture

        [HttpGet]
        public async Task<IActionResult> AssignedTests(
            CancellationToken cancellationToken)
        {
            try
            {
                var tests = await _labTechnicianData
                    .GetAssignedTestsAsync(cancellationToken);

                var completedTests = new HashSet<string>();

                foreach (var test in tests)
                {
                    if (_labTechnicianData.IsTestCompleted(
                        test.SampleReceiptID,
                        test.TestTypeID))
                    {
                        completedTests.Add(
                            $"{test.SampleReceiptID}-{test.TestTypeID}");
                    }
                }

                ViewBag.CompletedTests = completedTests;
                return View(tests);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"AssignedTests failed: {ex}");

                TempData["ErrorMessage"] = "Could not load assigned tests.";
                ViewBag.CompletedTests = new HashSet<string>();

                return View(new List<LabTechnicianQueueItem>());
            }
        }

        [HttpGet]
        public IActionResult CaptureResultsLookup()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CaptureResultsLookup(
            string registrationNumber,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(registrationNumber))
            {
                ModelState.AddModelError(
                    nameof(registrationNumber),
                    "Enter a patient registration number.");

                return View();
            }

            registrationNumber = registrationNumber.Trim();

            try
            {
                var matchingTests = await _labTechnicianData
                    .FindPatientTestsAsync(
                        registrationNumber,
                        cancellationToken);

                ViewBag.RegistrationNumber = registrationNumber;

                if (matchingTests.Count == 0)
                {
                    ViewBag.LookupMessage =
                        "No assigned tests were found for that registration number.";

                    return View();
                }

                ViewBag.MatchingTests = matchingTests;
                return View();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"CaptureResultsLookup failed: {ex}");

                ViewBag.LookupMessage =
                    "The patient lookup failed. Check the application logs.";
                ViewBag.RegistrationNumber = registrationNumber;

                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> CaptureResults(
            int sampleReceiptId,
            int testTypeId,
            CancellationToken cancellationToken)
        {
            if (sampleReceiptId <= 0 || testTypeId <= 0)
            {
                TempData["ErrorMessage"] =
                    "Choose an individual assigned test first.";

                return RedirectToAction(nameof(AssignedTests));
            }

            try
            {
                var model = await _labTechnicianData.GetCaptureTestAsync(
                    sampleReceiptId,
                    testTypeId,
                    cancellationToken);

                if (model == null)
                {
                    TempData["ErrorMessage"] =
                        "The selected test could not be loaded. It may not be assigned, may have been rejected, or may already be completed.";

                    return RedirectToAction(nameof(AssignedTests));
                }

                return View(model);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"CaptureResults GET failed: {ex}");

                TempData["ErrorMessage"] =
                    "Could not load the selected test. Check the application logs.";

                return RedirectToAction(nameof(AssignedTests));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CaptureResults(
            CaptureResultsViewModel model,
            CancellationToken cancellationToken)
        {
            if (model.SampleReceiptID <= 0 || model.TestTypeID <= 0)
            {
                TempData["ErrorMessage"] =
                    "The selected sample or test is invalid.";

                return RedirectToAction(nameof(AssignedTests));
            }

            if (string.IsNullOrWhiteSpace(model.ResultValue))
            {
                ModelState.AddModelError(
                    nameof(model.ResultValue),
                    "Enter a result before saving.");

                return await ReloadCaptureFormAsync(model, cancellationToken);
            }

            try
            {
                var saveResult = await _labTechnicianData.SaveTestResultAsync(
                    model.SampleReceiptID,
                    model.TestTypeID,
                    model.ResultValue,
                    model.TechnicianNotes,
                    cancellationToken);

                if (!saveResult.Success)
                {
                    ModelState.AddModelError(string.Empty, saveResult.Message);
                    return await ReloadCaptureFormAsync(model, cancellationToken);
                }

                TempData["SuccessMessage"] = saveResult.Message;
                return RedirectToAction(nameof(AssignedTests));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"CaptureResults POST failed: {ex}");

                ModelState.AddModelError(
                    string.Empty,
                    "Could not save the result. Check the application logs.");

                return await ReloadCaptureFormAsync(model, cancellationToken);
            }
        }

        private async Task<IActionResult> ReloadCaptureFormAsync(
            CaptureResultsViewModel submittedModel,
            CancellationToken cancellationToken)
        {
            try
            {
                var freshModel = await _labTechnicianData.GetCaptureTestAsync(
                    submittedModel.SampleReceiptID,
                    submittedModel.TestTypeID,
                    cancellationToken);

                if (freshModel == null)
                {
                    TempData["ErrorMessage"] =
                        "The selected test is no longer available.";

                    return RedirectToAction(nameof(AssignedTests));
                }

                freshModel.ResultValue = submittedModel.ResultValue;
                freshModel.TechnicianNotes = submittedModel.TechnicianNotes;

                return View(nameof(CaptureResults), freshModel);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ReloadCaptureForm failed: {ex}");

                TempData["ErrorMessage"] = "Could not reload the capture form.";
                return RedirectToAction(nameof(AssignedTests));
            }
        }

        // Result verification

        [HttpGet]
        public async Task<IActionResult> VerificationQueue(
            CancellationToken cancellationToken)
        {
            try
            {
                var results = await _labTechnicianData
                    .GetVerificationQueueAsync(cancellationToken);

                return View(results);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"VerificationQueue failed: {ex}");

                TempData["ErrorMessage"] =
                    "Could not load the verification queue. Check the application logs.";

                return View(new List<VerificationQueueItemViewModel>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> VerifyResults(
            int testResultId,
            CancellationToken cancellationToken)
        {
            if (testResultId <= 0)
            {
                TempData["ErrorMessage"] =
                    "Select a valid test result to verify.";

                return RedirectToAction(nameof(VerificationQueue));
            }

            try
            {
                var result = await _labTechnicianData
                    .GetVerificationResultAsync(
                        testResultId,
                        cancellationToken);

                if (result == null)
                {
                    TempData["ErrorMessage"] =
                        "That result could not be found in the current verification session.";

                    return RedirectToAction(nameof(VerificationQueue));
                }

                return View(result);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"VerifyResults failed: {ex}");

                TempData["ErrorMessage"] =
                    "Could not load the selected result. Check the application logs.";

                return RedirectToAction(nameof(VerificationQueue));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitVerificationDecision(
            int testResultId,
            string decision,
            string? returnReason,
            CancellationToken cancellationToken)
        {
            if (testResultId <= 0)
            {
                TempData["ErrorMessage"] = "Invalid test result.";
                return RedirectToAction(nameof(VerificationQueue));
            }

            var normalizedDecision = decision?.Trim();

            if (!string.Equals(
                    normalizedDecision,
                    "Approved",
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals(
                    normalizedDecision,
                    "Returned",
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] =
                    "Select a valid verification decision.";

                return RedirectToAction(
                    nameof(VerifyResults),
                    new { testResultId });
            }

            if (string.Equals(
                    normalizedDecision,
                    "Returned",
                    StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(returnReason))
            {
                TempData["ErrorMessage"] =
                    "A reason is required when returning a result for recapture.";

                return RedirectToAction(
                    nameof(VerifyResults),
                    new { testResultId });
            }

            try
            {
                var result = await _labTechnicianData
                    .GetVerificationResultAsync(
                        testResultId,
                        cancellationToken);

                if (result == null)
                {
                    TempData["ErrorMessage"] =
                        "The selected test result could not be found.";

                    return RedirectToAction(nameof(VerificationQueue));
                }

                TempData["ErrorMessage"] =
                    "The decision was not saved. This login must be linked to a valid lab technician before verification can be recorded.";

                return RedirectToAction(
                    nameof(VerifyResults),
                    new { testResultId });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"SubmitVerificationDecision failed: {ex}");

                TempData["ErrorMessage"] =
                    "Could not process the verification decision. Check the application logs.";

                return RedirectToAction(nameof(VerificationQueue));
            }
        }

        [HttpGet]
        public async Task<IActionResult> ReviewQueue(
            CancellationToken cancellationToken)
        {
            try
            {
                var results = await _labTechnicianData
                    .GetVerificationQueueAsync(cancellationToken);

                var returnedTests = results
                    .Where(x =>
                        string.Equals(
                            x.LatestVerificationDecision,
                            "Returned",
                            StringComparison.OrdinalIgnoreCase)
                        || string.Equals(
                            x.LatestVerificationDecision,
                            "Return",
                            StringComparison.OrdinalIgnoreCase)
                        || string.Equals(
                            x.LatestVerificationDecision,
                            "Rejected",
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return View(returnedTests);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ReviewQueue failed: {ex}");

                TempData["ErrorMessage"] =
                    "The Review Queue could not be loaded. Please try again.";

                return View(new List<VerificationQueueItemViewModel>());
            }
        }

        // Receive and reject samples

        [HttpGet]
        public async Task<IActionResult> ReceiveSamples(
            CancellationToken cancellationToken)
        {
            try
            {
                var pendingRequests = await _labTechnicianData
                    .GetRequestsAwaitingReceiptAsync(cancellationToken);

                var receivedSamples = await _labTechnicianData
                    .GetReceivedSamplesAsync(cancellationToken);

                var allRows = pendingRequests
                    .Concat(receivedSamples)
                    .OrderByDescending(x => x.DateTimeReceived.HasValue)
                    .ThenByDescending(x => x.DateTimeReceived)
                    .ThenByDescending(x => x.RequestDate)
                    .ToList();

                return View(allRows);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ReceiveSamples failed: {ex}");

                TempData["ErrorMessage"] =
                    "Could not load sample requests. Check the application logs.";

                return View(new List<LabTechnicianQueueItem>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceiveSample(
            int testRequestId,
            string barcode,
            CancellationToken cancellationToken)
        {
            if (testRequestId <= 0 || string.IsNullOrWhiteSpace(barcode))
            {
                TempData["ErrorMessage"] =
                    "A valid request and barcode are required.";

                return RedirectToAction(nameof(ReceiveSamples));
            }

            try
            {
                var received = await _labTechnicianData.ReceiveSampleAsync(
                    testRequestId,
                    barcode,
                    cancellationToken);

                TempData[received ? "SuccessMessage" : "ErrorMessage"] =
                    received
                        ? "Sample received successfully."
                        : "The request could not be found, or this barcode has already been received.";
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ReceiveSample failed: {ex}");

                TempData["ErrorMessage"] =
                    "Could not receive the sample. Check the application logs.";
            }

            return RedirectToAction(nameof(ReceiveSamples));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectSample(
            int sampleReceiptId,
            string reason,
            CancellationToken cancellationToken)
        {
            if (sampleReceiptId <= 0 || string.IsNullOrWhiteSpace(reason))
            {
                TempData["ErrorMessage"] =
                    "Select a sample and provide a rejection reason.";

                return RedirectToAction(nameof(ReceiveSamples));
            }

            try
            {
                var rejected = await _labTechnicianData.RejectSampleAsync(
                    sampleReceiptId,
                    reason,
                    cancellationToken);

                TempData[rejected ? "SuccessMessage" : "ErrorMessage"] =
                    rejected
                        ? "Sample rejected. Other samples on this request remain unaffected."
                        : "Sample not found. Refresh the page and try again.";
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"RejectSample failed: {ex}");

                TempData["ErrorMessage"] =
                    "Could not reject the sample. Check the application logs.";
            }

            return RedirectToAction(nameof(ReceiveSamples));
        }
    }
}