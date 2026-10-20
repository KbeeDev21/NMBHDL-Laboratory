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
    public sealed class StatUrgentTestViewModel
    {
        public int TestRequestID { get; set; }
        public int SampleReceiptID { get; set; }
        public int TestTypeID { get; set; }
        public int TestResultID { get; set; }
        public int PatientID { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public DateTime? RequestDate { get; set; }
        public string Urgency { get; set; } = string.Empty;
        public string RequestStatus { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public int TurnAroundMinutes { get; set; }
        public DateTime? DateTimeReceived { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PatientNumber { get; set; } = string.Empty;
        public string ResultStatus { get; set; } = string.Empty;
        public DateTime? CompletionDateTime { get; set; }
        public string LatestDecision { get; set; } = string.Empty;
        public string Stage { get; set; } = string.Empty;
        public DateTime? DueDateTime { get; set; }
        public string TatStatus { get; set; } = string.Empty;
    }

    public sealed class TestDetailViewModel
    {
        public int TestRequestID { get; set; }
        public int SampleReceiptID { get; set; }
        public int PatientID { get; set; }
        public string RequestNumber => $"REQ-{TestRequestID:D5}";
        public string Barcode { get; set; } = string.Empty;
        public string SampleType { get; set; } = "Not specified";
        public DateTime? RequestDate { get; set; }
        public DateTime? DateTimeReceived { get; set; }
        public string Urgency { get; set; } = string.Empty;
        public string RequestStatus { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string PatientNumber { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string RequestedTypeText { get; set; } = string.Empty;
        public List<string> RequestedTests { get; set; } = new();
        public bool HasActiveSample { get; set; }
        public bool IsRejectedSample { get; set; }
        public bool IsSelectedTest { get; set; }
        public bool HasResult { get; set; }
        public bool IsComplete { get; set; }
        public int TestTypeID { get; set; }
        public string SelectedTestName { get; set; } = string.Empty;
        public string StageMessage { get; set; } = string.Empty;
    }

    public class LabTechnicianController : Controller
    {
        private readonly LabTechnicianDataService _labTechnicianData;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public LabTechnicianController(
            LabTechnicianDataService labTechnicianData,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _labTechnicianData = labTechnicianData;
            _configuration = configuration;
            _environment = environment;
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
        public async Task<IActionResult> StatUrgent(
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

                const string sql = """
                    SELECT
                        tr.TestRequestID,
                        COALESCE(sr.SampleReceiptID, 0) AS SampleReceiptID,
                        COALESCE(tt.TestTypeID, 0) AS TestTypeID,
                        COALESCE(sr.Barcode, tr.Barcode, '') AS Barcode,
                        tr.RequestDate,
                        COALESCE(tr.Urgency, '') AS Urgency,
                        COALESCE(tr.Status, '') AS RequestStatus,
                        COALESCE(tt.TestName, LTRIM(RTRIM(requested.value)), '') AS TestName,
                        COALESCE(tt.TurnAroundMinutes, 0) AS TurnAroundMinutes,
                        sr.DateTimeReceived,
                        p.PatientID,
                        LTRIM(RTRIM(CONCAT(u.Surname, ', ', u.Name))) AS PatientName,
                        COALESCE(u.IDNumber, '') AS PatientNumber,
                        COALESCE(latestResult.TestResultID, 0) AS TestResultID,
                        COALESCE(latestResult.ResultStatus, '') AS ResultStatus,
                        latestResult.CompletionDateTime,
                        COALESCE(latestDecision.Decision, '') AS LatestDecision,
                        CASE
                            WHEN COALESCE(latestDecision.Decision, '') IN
                                ('Returned', 'Return', 'Rejected')
                                THEN 'Review'
                            WHEN COALESCE(latestDecision.Decision, '') IN
                                ('Approved', 'Verified')
                                THEN 'Complete'
                            WHEN latestResult.TestResultID IS NOT NULL
                                THEN 'Verification'
                            WHEN COALESCE(tr.Status, '') IN ('Completed', 'Verified')
                                THEN 'Complete'
                            WHEN COALESCE(tr.Status, '') IN ('In Progress', 'Processing')
                                THEN 'Processing'
                            WHEN sr.SampleReceiptID IS NOT NULL
                                THEN 'Sample Received'
                            ELSE 'Awaiting Sample'
                        END AS Stage
                    FROM dbo.TestRequest tr
                    INNER JOIN dbo.Patient p
                        ON p.PatientID = tr.PatientID
                    INNER JOIN dbo.[User] u
                        ON u.UserID = p.UserID
                    CROSS APPLY STRING_SPLIT(
                        REPLACE(
                            REPLACE(
                                REPLACE(
                                    REPLACE(COALESCE(tr.Type, ''), ';', ','),
                                    '+', ','
                                ),
                                CHAR(13), ','
                            ),
                            CHAR(10), ','
                        ),
                        ','
                    ) requested
                    LEFT JOIN dbo.TestType tt
                        ON LTRIM(RTRIM(requested.value)) = LTRIM(RTRIM(tt.TestName))
                    LEFT JOIN dbo.SampleReceipt sr
                        ON sr.TestRequestID = tr.TestRequestID
                       AND NOT EXISTS
                       (
                           SELECT 1
                           FROM dbo.SampleReceiptRejection rejected
                           WHERE rejected.SampleReceiptID = sr.SampleReceiptID
                       )
                    OUTER APPLY
                    (
                        SELECT TOP (1)
                            result.TestResultID,
                            COALESCE(result.Status, '') AS ResultStatus,
                            result.CompletionDateTime
                        FROM dbo.TestResult result
                        WHERE result.TestRequestID = tr.TestRequestID
                          AND result.TestTypeID = tt.TestTypeID
                        ORDER BY result.CompletionDateTime DESC, result.TestResultID DESC
                    ) latestResult
                    OUTER APPLY
                    (
                        SELECT TOP (1)
                            verification.Decision
                        FROM dbo.ResultVerification verification
                        WHERE verification.TestResultID = latestResult.TestResultID
                        ORDER BY verification.VerificationDateTime DESC,
                                 verification.ResultVerificationID DESC
                    ) latestDecision
                    WHERE UPPER(LTRIM(RTRIM(COALESCE(tr.Urgency, ''))))
                        IN ('STAT', 'URGENT')
                      AND COALESCE(tr.Status, '') NOT IN
                        ('Cancelled', 'Canceled')
                    ORDER BY
                        CASE
                            WHEN UPPER(LTRIM(RTRIM(COALESCE(tr.Urgency, '')))) = 'STAT'
                                THEN 0
                            ELSE 1
                        END,
                        tr.RequestDate ASC,
                        tr.TestRequestID,
                        tt.TestName;
                    """;

                await using var connection = new SqlConnection(connectionString);

                var rows = (await connection.QueryAsync<StatUrgentTestViewModel>(
                    new CommandDefinition(
                        sql,
                        cancellationToken: cancellationToken))).ToList();

                var assignedTests = await _labTechnicianData
                    .GetAssignedTestsAsync(cancellationToken);

                var assignedKeys = new HashSet<string>(
                    assignedTests
                        .Where(test => !_labTechnicianData.IsTestCompleted(
                            test.SampleReceiptID, test.TestTypeID))
                        .Select(test => $"{test.SampleReceiptID}:{test.TestTypeID}"),
                    StringComparer.OrdinalIgnoreCase);

                var now = DateTime.Now;

                foreach (var test in rows)
                {
                    if (test.SampleReceiptID > 0 && test.TestTypeID > 0 &&
                        assignedKeys.Contains($"{test.SampleReceiptID}:{test.TestTypeID}") &&
                        test.TestResultID == 0)
                    {
                        test.Stage = "Processing";
                    }

                    if (test.DateTimeReceived.HasValue &&
                        test.TurnAroundMinutes > 0 &&
                        test.TestResultID == 0 &&
                        !string.Equals(test.Stage, "Complete",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        test.DueDateTime = test.DateTimeReceived.Value
                            .AddMinutes(test.TurnAroundMinutes);

                        var minutesRemaining =
                            (test.DueDateTime.Value - now).TotalMinutes;

                        test.TatStatus = minutesRemaining < 0
                            ? "Breached"
                            : minutesRemaining <= Math.Max(15, test.TurnAroundMinutes * 0.2)
                                ? "At Risk"
                                : "Within TAT";
                    }
                    else if (test.TestResultID > 0 ||
                             string.Equals(test.Stage, "Complete",
                                 StringComparison.OrdinalIgnoreCase))
                    {
                        test.TatStatus = "Complete";
                    }
                    else
                    {
                        test.TatStatus = "Unknown";
                    }
                }

                ViewBag.TatBreachedCount = rows.Count(x => x.TatStatus == "Breached");
                ViewBag.AtRiskCount = rows.Count(x => x.TatStatus == "At Risk");
                ViewBag.ActiveStatCount = rows.Count(x =>
                    string.Equals(x.Urgency, "STAT",
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(x.Stage, "Complete",
                        StringComparison.OrdinalIgnoreCase));
                ViewBag.CompletedTodayCount = rows.Count(x =>
                    x.CompletionDateTime.HasValue &&
                    x.CompletionDateTime.Value.Date == now.Date);
                var completedToday = rows
                    .Where(x => x.CompletionDateTime.HasValue &&
                                x.DateTimeReceived.HasValue &&
                                x.CompletionDateTime.Value.Date == now.Date &&
                                x.CompletionDateTime.Value >= x.DateTimeReceived.Value)
                    .Select(x => (x.CompletionDateTime!.Value -
                                  x.DateTimeReceived!.Value).TotalMinutes)
                    .ToList();
                ViewBag.AverageTatToday = completedToday.Count == 0
                    ? "—"
                    : $"{Math.Round(completedToday.Average())}m";

                return View(rows);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"StatUrgent failed: {ex}");
                TempData["ErrorMessage"] =
                    "Could not load STAT and urgent tests from the database. Check the application logs.";

                ViewBag.TatBreachedCount = 0;
                ViewBag.AtRiskCount = 0;
                ViewBag.ActiveStatCount = 0;
                ViewBag.CompletedTodayCount = 0;
                ViewBag.AverageTatToday = "—";

                return View(new List<StatUrgentTestViewModel>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> TestDetail(
            int id,
            int testRequestId = 0,
            int sampleReceiptId = 0,
            int testTypeId = 0,
            CancellationToken cancellationToken = default)
        {
            // Support the common route/query names used by existing links.
            var requestId = testRequestId > 0 ? testRequestId : id;
            if (requestId <= 0)
            {
                TempData["ErrorMessage"] = "Select a valid test request first.";
                return RedirectToAction(nameof(ReceiveSamples));
            }

            try
            {
                const string sql = """
                    SELECT
                        tr.TestRequestID,
                        tr.PatientID,
                        tr.RequestDate,
                        COALESCE(tr.Urgency, '') AS Urgency,
                        COALESCE(tr.Status, '') AS RequestStatus,
                        COALESCE(tr.Type, '') AS RequestedTypeText,
                        COALESCE(tr.Barcode, '') AS RequestBarcode,
                        LTRIM(RTRIM(CONCAT(u.Surname, ', ', u.Name))) AS PatientName,
                        COALESCE(u.IDNumber, '') AS PatientNumber,
                        LTRIM(RTRIM(CONCAT(
                            COALESCE(doctor.Surname, ''),
                            CASE WHEN doctor.Surname IS NOT NULL THEN ', ' ELSE '' END,
                            COALESCE(doctor.Name, '')
                        ))) AS DoctorName,
                        COALESCE(sr.SampleReceiptID, 0) AS SampleReceiptID,
                        COALESCE(sr.Barcode, tr.Barcode, '') AS SampleBarcode,
                        sr.DateTimeReceived,
                        CAST(CASE WHEN rejection.SampleReceiptID IS NULL THEN 0 ELSE 1 END AS bit) AS IsRejectedSample
                    FROM dbo.TestRequest tr
                    INNER JOIN dbo.Patient p ON p.PatientID = tr.PatientID
                    INNER JOIN dbo.[User] u ON u.UserID = p.UserID
                    LEFT JOIN dbo.Doctor d ON d.DoctorID = tr.DoctorID
                    LEFT JOIN dbo.[User] doctor ON doctor.UserID = d.UserID
                    OUTER APPLY
                    (
                        SELECT TOP (1) receipt.*
                        FROM dbo.SampleReceipt receipt
                        WHERE receipt.TestRequestID = tr.TestRequestID
                          AND (@SampleReceiptID = 0 OR receipt.SampleReceiptID = @SampleReceiptID)
                        ORDER BY
                            CASE WHEN EXISTS
                            (
                                SELECT 1
                                FROM dbo.SampleReceiptRejection r
                                WHERE r.SampleReceiptID = receipt.SampleReceiptID
                            ) THEN 1 ELSE 0 END,
                            receipt.DateTimeReceived DESC,
                            receipt.SampleReceiptID DESC
                    ) sr
                    LEFT JOIN dbo.SampleReceiptRejection rejection
                        ON rejection.SampleReceiptID = sr.SampleReceiptID
                    WHERE tr.TestRequestID = @TestRequestID;
                    """;

                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(connectionString))
                    throw new InvalidOperationException("The DefaultConnection connection string is missing.");

                await using var connection = new SqlConnection(connectionString);
                var row = await connection.QuerySingleOrDefaultAsync<TestDetailQueryRow>(
                    new CommandDefinition(
                        sql,
                        new { TestRequestID = requestId, SampleReceiptID = sampleReceiptId },
                        cancellationToken: cancellationToken));

                if (row == null)
                {
                    TempData["ErrorMessage"] = "The selected test request could not be found.";
                    return RedirectToAction(nameof(ReceiveSamples));
                }

                var model = new TestDetailViewModel
                {
                    TestRequestID = row.TestRequestID,
                    SampleReceiptID = row.SampleReceiptID,
                    PatientID = row.PatientID,
                    Barcode = row.SampleBarcode,
                    RequestDate = row.RequestDate,
                    DateTimeReceived = row.DateTimeReceived,
                    Urgency = row.Urgency,
                    RequestStatus = row.RequestStatus,
                    PatientName = row.PatientName,
                    PatientNumber = row.PatientNumber,
                    DoctorName = string.IsNullOrWhiteSpace(row.DoctorName) ? "Not specified" : row.DoctorName,
                    RequestedTypeText = row.RequestedTypeText,
                    HasActiveSample = row.SampleReceiptID > 0 && !row.IsRejectedSample,
                    IsRejectedSample = row.IsRejectedSample
                };

                model.RequestedTests = (row.RequestedTypeText ?? string.Empty)
                    .Split(new[] { ',', ';', '+', '\r', '\n' },
                        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (testTypeId > 0 && model.SampleReceiptID > 0 && model.HasActiveSample)
                {
                    const string selectedSql = """
                        SELECT
                            tt.TestTypeID,
                            COALESCE(tt.TestName, '') AS TestName,
                            COALESCE(latest.TestResultID, 0) AS TestResultID,
                            COALESCE(latest.Decision, '') AS LatestDecision
                        FROM dbo.TestType tt
                        OUTER APPLY
                        (
                            SELECT TOP (1)
                                result.TestResultID,
                                verification.Decision
                            FROM dbo.TestResult result
                            OUTER APPLY
                            (
                                SELECT TOP (1) rv.Decision
                                FROM dbo.ResultVerification rv
                                WHERE rv.TestResultID = result.TestResultID
                                ORDER BY rv.VerificationDateTime DESC, rv.ResultVerificationID DESC
                            ) verification
                            WHERE result.TestRequestID = @TestRequestID
                              AND result.TestTypeID = tt.TestTypeID
                            ORDER BY result.CompletionDateTime DESC, result.TestResultID DESC
                        ) latest
                        WHERE tt.TestTypeID = @TestTypeID;
                        """;

                    var selected = await connection.QuerySingleOrDefaultAsync<TestDetailSelectedRow>(
                        new CommandDefinition(
                            selectedSql,
                            new { TestRequestID = requestId, TestTypeID = testTypeId },
                            cancellationToken: cancellationToken));

                    if (selected != null &&
                        model.RequestedTests.Contains(selected.TestName, StringComparer.OrdinalIgnoreCase))
                    {
                        model.IsSelectedTest = true;
                        model.TestTypeID = selected.TestTypeID;
                        model.SelectedTestName = selected.TestName;
                        model.HasResult = selected.TestResultID > 0;
                        model.IsComplete = string.Equals(selected.LatestDecision, "Approved", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(selected.LatestDecision, "Verified", StringComparison.OrdinalIgnoreCase);

                        model.StageMessage = model.IsComplete
                            ? "This test has already been verified."
                            : model.HasResult && (string.Equals(selected.LatestDecision, "Returned", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(selected.LatestDecision, "Return", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(selected.LatestDecision, "Rejected", StringComparison.OrdinalIgnoreCase))
                                ? "This result was returned and is available for recapture."
                                : model.HasResult
                                    ? "This test has a result awaiting verification."
                                    : "This test is selected and ready for result capture.";
                    }
                }

                return View(model);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"TestDetail failed: {ex}");
                TempData["ErrorMessage"] = "Could not load the selected test details. Check the application logs.";
                return RedirectToAction(nameof(ReceiveSamples));
            }
        }

        private sealed class TestDetailQueryRow
        {
            public int TestRequestID { get; set; }
            public int PatientID { get; set; }
            public DateTime? RequestDate { get; set; }
            public string Urgency { get; set; } = string.Empty;
            public string RequestStatus { get; set; } = string.Empty;
            public string RequestedTypeText { get; set; } = string.Empty;
            public string PatientName { get; set; } = string.Empty;
            public string PatientNumber { get; set; } = string.Empty;
            public string DoctorName { get; set; } = string.Empty;
            public int SampleReceiptID { get; set; }
            public string SampleBarcode { get; set; } = string.Empty;
            public DateTime? DateTimeReceived { get; set; }
            public bool IsRejectedSample { get; set; }
        }

        private sealed class TestDetailSelectedRow
        {
            public int TestTypeID { get; set; }
            public string TestName { get; set; } = string.Empty;
            public int TestResultID { get; set; }
            public string LatestDecision { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> Escalations(
    CancellationToken cancellationToken)
        {
            try
            {
                var escalations = await _labTechnicianData
                    .GetActiveEscalationsAsync(cancellationToken);

                return View(escalations);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Escalations failed: {ex}");

                TempData["ErrorMessage"] =
                    $"Escalations error: {ex.Message}";

                return View(new List<LabEscalationViewModel>());
            }
        }

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

                var pendingResults = results
                    .Where(x =>
                        !string.Equals(x.LatestVerificationDecision, "Approved",
                            StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(x.LatestVerificationDecision, "Returned",
                            StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(x.LatestVerificationDecision, "Return",
                            StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(x.LatestVerificationDecision, "Rejected",
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return View(pendingResults);
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

            if (!string.Equals(normalizedDecision, "Approved",
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalizedDecision, "Returned",
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Select a valid verification decision.";
                return RedirectToAction(nameof(VerifyResults), new { testResultId });
            }

            if (string.Equals(normalizedDecision, "Returned",
                    StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(returnReason))
            {
                TempData["ErrorMessage"] =
                    "A reason is required when returning a result for recapture.";
                return RedirectToAction(nameof(VerifyResults), new { testResultId });
            }

            try
            {
                var result = await _labTechnicianData.GetVerificationResultAsync(
                    testResultId, cancellationToken);

                if (result == null)
                {
                    TempData["ErrorMessage"] = "The selected test result was not found.";
                    return RedirectToAction(nameof(VerificationQueue));
                }

                var userId = User.FindFirst(
                    System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;


                if (!int.TryParse(userId, out var loggedInUserId))
                {
                    var developmentUserId =
                        _configuration.GetValue<int?>(
                            "LabTechnician:DevelopmentUserId");

                    if (!_environment.IsDevelopment()
                        || developmentUserId is null
                        || developmentUserId.Value <= 0)
                    {
                        TempData["ErrorMessage"] =
                            "Your login could not be matched to a lab technician.";

                        return RedirectToAction(
                            nameof(VerifyResults),
                            new { testResultId });
                    }

                    loggedInUserId = developmentUserId.Value;
                }


                var technicianId = await _labTechnicianData
                    .GetLabTechnicianIdForUserAsync(loggedInUserId, cancellationToken);

                if (technicianId == null || technicianId.Value <= 0)
                {
                    TempData["ErrorMessage"] =
                        "No lab technician profile is linked to your login.";
                    return RedirectToAction(nameof(VerifyResults), new { testResultId });
                }

                var saveResult = await _labTechnicianData.SaveVerificationDecisionAsync(
                    testResultId,
                    technicianId.Value,
                    normalizedDecision!,
                    returnReason,
                    cancellationToken);

                if (!saveResult.Success)
                {
                    Console.Error.WriteLine(
                        $"Verification save failed for TestResultID {testResultId}: {saveResult.Message}");

                    TempData["ErrorMessage"] = saveResult.Message;
                    return RedirectToAction(nameof(VerifyResults), new { testResultId });
                }

                Console.WriteLine(
                    $"Verification save succeeded for TestResultID {testResultId}: {saveResult.Message}");

                TempData["SuccessMessage"] = saveResult.Message;

                // After a result is returned, stay on the Verification Queue.
                // The returned result can be opened for recapture from Review Queue.
                return RedirectToAction(nameof(VerificationQueue));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"SubmitVerificationDecision failed: {ex}");
                TempData["ErrorMessage"] =
                    "Could not save the verification decision. Check the application logs.";
                return RedirectToAction(nameof(VerifyResults), new { testResultId });
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

                // Pass all results to the view. ReviewQueue.cshtml must first
                // select the newest result for each request/test type and only
                // then filter for Returned/Return/Rejected. Filtering here first
                // would leave an older returned result visible after recapture.
                return View(results);
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