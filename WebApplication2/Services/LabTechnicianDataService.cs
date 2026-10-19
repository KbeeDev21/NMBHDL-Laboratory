using System.Collections.Concurrent;
using System.Globalization;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using WebApplication2.Models;

namespace WebApplication2.Services;

public sealed class LabTechnicianDataService
{
    private readonly string _connectionString;

    private static readonly ConcurrentDictionary<int, string> Rejections = new();
    private static readonly ConcurrentDictionary<string, Assignment> Assignments = new();
    private static readonly ConcurrentDictionary<string, CompletedTest> CompletedTests = new();

    private sealed record Assignment(
        int SampleReceiptID,
        int TestRequestID,
        int TestTypeID,
        string TestName,
        DateTime SelectedAt,
        int TurnAroundMinutes);

    private sealed record CompletedTest(
        int TestResultID,
        int SampleReceiptID,
        int TestRequestID,
        int TestTypeID,
        string TestName,
        string Result,
        string Status,
        DateTime CompletionDateTime);

    private sealed class CaptureTestRow
    {
        public int SampleReceiptID { get; set; }
        public int TestRequestID { get; set; }
        public int TestTypeID { get; set; }
        public int PatientID { get; set; }
        public string Barcode { get; set; } = "";
        public string PatientName { get; set; } = "";
        public string PatientNumber { get; set; } = "";
        public string TestName { get; set; } = "";
        public string Units { get; set; } = "";
        public string Urgency { get; set; } = "";
        public string RequestStatus { get; set; } = "";
        public DateTime RequestDate { get; set; }
        public DateTime? DateTimeReceived { get; set; }
        public decimal? NormalRangeMin { get; set; }
        public decimal? NormalRangeMax { get; set; }
        public int TurnAroundMinutes { get; set; }
    }

    private sealed class TestRangeRow
    {
        public decimal? NormalRangeMin { get; set; }
        public decimal? NormalRangeMax { get; set; }
    }

    public LabTechnicianDataService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Missing DefaultConnection connection string.");
    }

    public async Task<IReadOnlyList<LabTechnicianQueueItem>>
        GetRequestsAwaitingReceiptAsync(
            CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT tr.TestRequestID,
                   COALESCE(tr.barcode, '') AS Barcode,
                   tr.RequestDate,
                   COALESCE(tr.Urgency, '') AS Urgency,
                   COALESCE(tr.Status, '') AS Status,
                   COALESCE(tr.Type, '') AS Type,
                   p.PatientID,
                   LTRIM(RTRIM(CONCAT(u.Surname, ', ', u.Name))) AS PatientName,
                   COALESCE(u.IDNumber, '') AS PatientNumber,
                   CAST(NULL AS datetime2) AS DateTimeReceived
            FROM dbo.TestRequest tr
            INNER JOIN dbo.Patient p ON p.PatientID = tr.PatientID
            INNER JOIN dbo.[User] u ON u.UserID = p.UserID
            WHERE COALESCE(tr.Status, '') NOT IN
                ('Cancelled', 'Canceled', 'Completed', 'Awaiting Selection')
              AND NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.SampleReceipt sr
                    WHERE sr.TestRequestID = tr.TestRequestID
                )
            ORDER BY
                CASE
                    WHEN UPPER(LTRIM(RTRIM(COALESCE(tr.Urgency, '')))) = 'STAT'
                    THEN 0
                    WHEN UPPER(LTRIM(RTRIM(COALESCE(tr.Urgency, '')))) = 'URGENT'
                    THEN 1
                    ELSE 2
                END,
                tr.RequestDate DESC;
            """;

        await using var connection = new SqlConnection(_connectionString);

        var rows = await connection.QueryAsync<LabTechnicianQueueItem>(
            new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<LabTechnicianQueueItem>>
        GetReceivedSamplesAsync(
            CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT sr.SampleReceiptID,
                   tr.TestRequestID,
                   COALESCE(sr.Barcode, '') AS Barcode,
                   tr.RequestDate,
                   COALESCE(tr.Urgency, '') AS Urgency,
                   COALESCE(tr.Status, '') AS Status,
                   COALESCE(tr.Type, '') AS Type,
                   p.PatientID,
                   LTRIM(RTRIM(CONCAT(u.Surname, ', ', u.Name))) AS PatientName,
                   COALESCE(u.IDNumber, '') AS PatientNumber,
                   sr.DateTimeReceived
            FROM dbo.SampleReceipt sr
            INNER JOIN dbo.TestRequest tr ON tr.TestRequestID = sr.TestRequestID
            INNER JOIN dbo.Patient p ON p.PatientID = tr.PatientID
            INNER JOIN dbo.[User] u ON u.UserID = p.UserID
            ORDER BY sr.DateTimeReceived DESC;
            """;

        await using var connection = new SqlConnection(_connectionString);

        var rows = (await connection.QueryAsync<LabTechnicianQueueItem>(
            new CommandDefinition(
                sql,
                cancellationToken: cancellationToken)))
            .AsList();

        foreach (var sample in rows)
        {
            if (Rejections.TryGetValue(sample.SampleReceiptID, out var reason))
            {
                sample.IsRejected = true;
                sample.RejectionReason = reason;
            }
        }

        return rows;
    }

    public async Task<bool> ReceiveSampleAsync(
        int testRequestId,
        string barcode,
        CancellationToken cancellationToken = default)
    {
        if (testRequestId <= 0 || string.IsNullOrWhiteSpace(barcode))
            return false;

        const string sql = """
            SET XACT_ABORT ON;

            BEGIN TRY
                BEGIN TRANSACTION;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.TestRequest WITH (UPDLOCK, HOLDLOCK)
                    WHERE TestRequestID = @TestRequestID
                      AND COALESCE(Status, '') NOT IN
                          ('Cancelled', 'Canceled', 'Completed', 'Awaiting Selection')
                )
                BEGIN
                    ROLLBACK TRANSACTION;
                    SELECT CAST(0 AS bit);
                END
                ELSE IF EXISTS
                (
                    SELECT 1
                    FROM dbo.SampleReceipt WITH (UPDLOCK, HOLDLOCK)
                    WHERE Barcode = @Barcode
                )
                BEGIN
                    ROLLBACK TRANSACTION;
                    SELECT CAST(0 AS bit);
                END
                ELSE
                BEGIN
                    INSERT INTO dbo.SampleReceipt
                        (Barcode, DateTimeReceived, TestRequestID)
                    VALUES
                        (@Barcode, SYSDATETIME(), @TestRequestID);

                    COMMIT TRANSACTION;
                    SELECT CAST(1 AS bit);
                END
            END TRY
            BEGIN CATCH
                IF @@TRANCOUNT > 0
                    ROLLBACK TRANSACTION;

                THROW;
            END CATCH;
            """;

        await using var connection = new SqlConnection(_connectionString);

        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new
                {
                    TestRequestID = testRequestId,
                    Barcode = barcode.Trim()
                },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> RejectSampleAsync(
        int sampleReceiptId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (sampleReceiptId <= 0 || string.IsNullOrWhiteSpace(reason))
            return false;

        const string sql = """
            SELECT COUNT(1)
            FROM dbo.SampleReceipt
            WHERE SampleReceiptID = @SampleReceiptID;
            """;

        await using var connection = new SqlConnection(_connectionString);

        var exists = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new { SampleReceiptID = sampleReceiptId },
                cancellationToken: cancellationToken));

        if (exists == 0)
            return false;

        Rejections[sampleReceiptId] = reason.Trim();
        return true;
    }

    public async Task<IReadOnlyList<LabTechnicianQueueItem>>
        GetAvailableTestTypesAsync(
            CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TestTypeID,
                   COALESCE(TestName, '') AS TestName,
                   COALESCE(TurnAroundMinutes, 0) AS TurnAroundMinutes
            FROM dbo.TestType
            ORDER BY TestName;
            """;

        await using var connection = new SqlConnection(_connectionString);

        var rows = await connection.QueryAsync<LabTechnicianQueueItem>(
            new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<ConsumableStockItem>>
        GetConsumableStockAsync(
            CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT ConsumableID,
                   COALESCE(Name, '') AS Name,
                   COALESCE(QtyOnHand, 0) AS QtyOnHand,
                   COALESCE(ReOrderLevel, 0) AS ReOrderLevel
            FROM dbo.Consumable
            ORDER BY Name;
            """;

        await using var connection = new SqlConnection(_connectionString);

        var rows = await connection.QueryAsync<ConsumableStockItem>(
            new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<bool> IsConsumableInStockAsync(
        int consumableId,
        int requiredQuantity = 1,
        CancellationToken cancellationToken = default)
    {
        if (consumableId <= 0 || requiredQuantity <= 0)
            return false;

        const string sql = """
            SELECT CASE
                WHEN QtyOnHand >= @RequiredQuantity
                THEN CAST(1 AS bit)
                ELSE CAST(0 AS bit)
            END
            FROM dbo.Consumable
            WHERE ConsumableID = @ConsumableID;
            """;

        await using var connection = new SqlConnection(_connectionString);

        var result = await connection.QuerySingleOrDefaultAsync<bool?>(
            new CommandDefinition(
                sql,
                new
                {
                    ConsumableID = consumableId,
                    RequiredQuantity = requiredQuantity
                },
                cancellationToken: cancellationToken));

        return result == true;
    }

    public async Task<(bool Success, string Message)> SelectTestAsync(
        int sampleReceiptId,
        int testTypeId,
        CancellationToken cancellationToken = default)
    {
        if (sampleReceiptId <= 0 || testTypeId <= 0)
            return (false, "Select a valid sample and test.");

        const string sql = """
            SELECT sr.SampleReceiptID,
                   tr.TestRequestID,
                   COALESCE(tr.Type, '') AS RequestType,
                   COALESCE(tr.Status, '') AS RequestStatus,
                   COALESCE(tt.TestName, '') AS TestName,
                   COALESCE(tt.TurnAroundMinutes, 0) AS TurnAroundMinutes
            FROM dbo.SampleReceipt sr
            INNER JOIN dbo.TestRequest tr
                ON tr.TestRequestID = sr.TestRequestID
            CROSS JOIN dbo.TestType tt
            WHERE sr.SampleReceiptID = @SampleReceiptID
              AND tt.TestTypeID = @TestTypeID;
            """;

        await using var connection = new SqlConnection(_connectionString);

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(
                sql,
                new
                {
                    SampleReceiptID = sampleReceiptId,
                    TestTypeID = testTypeId
                },
                cancellationToken: cancellationToken));

        if (row == null)
            return (false, "Sample or test type was not found.");

        string requestType = (string)row.RequestType;
        string testName = (string)row.TestName;
        string requestStatus = (string)row.RequestStatus;

        if (Rejections.ContainsKey(sampleReceiptId))
            return (false, "This sample has been rejected.");

        if (requestStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) ||
            requestStatus.Equals("Canceled", StringComparison.OrdinalIgnoreCase) ||
            requestStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "This request is no longer available for assignment.");
        }

        var requestedNames = requestType.Split(
            new[] { ',', ';', '+', '\n', '\r' },
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (!requestedNames.Contains(testName, StringComparer.OrdinalIgnoreCase))
        {
            return (false,
                "This test could not be matched to the request's Type field. No assignment was made.");
        }

        string key = $"{sampleReceiptId}:{testTypeId}";

        if (CompletedTests.ContainsKey(key))
            return (false, "This test has already been completed.");

        var assignment = new Assignment(
            sampleReceiptId,
            (int)row.TestRequestID,
            testTypeId,
            testName,
            DateTime.Now,
            (int)row.TurnAroundMinutes);

        if (!Assignments.TryAdd(key, assignment))
            return (false, "This test has already been assigned to this sample.");

        return (true, $"'{testName}' assigned. Its turnaround timer has started.");
    }

    public async Task<IReadOnlyList<LabTechnicianQueueItem>>
        GetAssignedTestsAsync(
            CancellationToken cancellationToken = default)
    {
        var assignments = Assignments.Values
            .OrderByDescending(x => x.SelectedAt)
            .ToList();

        if (assignments.Count == 0)
            return Array.Empty<LabTechnicianQueueItem>();

        var samples = await GetReceivedSamplesAsync(cancellationToken);
        var result = new List<LabTechnicianQueueItem>();

        foreach (var assignment in assignments)
        {
            var sample = samples.FirstOrDefault(
                x => x.SampleReceiptID == assignment.SampleReceiptID);

            if (sample == null)
                continue;

            sample.TestTypeID = assignment.TestTypeID;
            sample.TestName = assignment.TestName;
            sample.SelectedAt = assignment.SelectedAt;
            sample.TurnAroundMinutes = assignment.TurnAroundMinutes;
            sample.IsSelected = true;

            result.Add(sample);
        }

        return result;
    }

    public async Task<IReadOnlyList<LabTechnicianQueueItem>>
        FindPatientTestsAsync(
            string registrationNumber,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(registrationNumber))
            return Array.Empty<LabTechnicianQueueItem>();

        const string sql = """
            SELECT sr.SampleReceiptID,
                   tr.TestRequestID,
                   COALESCE(sr.Barcode, '') AS Barcode,
                   tr.RequestDate,
                   COALESCE(tr.Urgency, '') AS Urgency,
                   COALESCE(tr.Status, '') AS Status,
                   COALESCE(tr.Type, '') AS Type,
                   p.PatientID,
                   LTRIM(RTRIM(CONCAT(u.Surname, ', ', u.Name))) AS PatientName,
                   COALESCE(u.IDNumber, '') AS PatientNumber,
                   sr.DateTimeReceived
            FROM dbo.[User] u
            INNER JOIN dbo.Patient p ON p.UserID = u.UserID
            INNER JOIN dbo.TestRequest tr ON tr.PatientID = p.PatientID
            INNER JOIN dbo.SampleReceipt sr ON sr.TestRequestID = tr.TestRequestID
            WHERE LTRIM(RTRIM(COALESCE(u.IDNumber, ''))) = @RegistrationNumber
              AND COALESCE(tr.Status, '') NOT IN
                  ('Cancelled', 'Canceled', 'Completed')
            ORDER BY tr.RequestDate DESC, sr.DateTimeReceived DESC;
            """;

        await using var connection = new SqlConnection(_connectionString);

        var samples = (await connection.QueryAsync<LabTechnicianQueueItem>(
            new CommandDefinition(
                sql,
                new { RegistrationNumber = registrationNumber.Trim() },
                cancellationToken: cancellationToken))).AsList();

        var result = new List<LabTechnicianQueueItem>();

        foreach (var sample in samples)
        {
            if (Rejections.TryGetValue(sample.SampleReceiptID, out var reason))
            {
                sample.IsRejected = true;
                sample.RejectionReason = reason;
            }

            var assignments = Assignments.Values
                .Where(a => a.SampleReceiptID == sample.SampleReceiptID)
                .OrderBy(a => a.TestName)
                .ToList();

            foreach (var assignment in assignments)
            {
                result.Add(new LabTechnicianQueueItem
                {
                    SampleReceiptID = sample.SampleReceiptID,
                    TestRequestID = sample.TestRequestID,
                    Barcode = sample.Barcode,
                    RequestDate = sample.RequestDate,
                    Urgency = sample.Urgency,
                    Status = sample.Status,
                    Type = sample.Type,
                    PatientID = sample.PatientID,
                    PatientName = sample.PatientName,
                    PatientNumber = sample.PatientNumber,
                    DateTimeReceived = sample.DateTimeReceived,
                    IsRejected = sample.IsRejected,
                    RejectionReason = sample.RejectionReason,
                    TestTypeID = assignment.TestTypeID,
                    TestName = assignment.TestName,
                    SelectedAt = assignment.SelectedAt,
                    TurnAroundMinutes = assignment.TurnAroundMinutes,
                    IsSelected = true
                });
            }
        }

        return result;
    }

    public async Task<CaptureResultsViewModel?> GetCaptureTestAsync(
        int sampleReceiptId,
        int testTypeId,
        CancellationToken cancellationToken = default)
    {
        string key = $"{sampleReceiptId}:{testTypeId}";

        if (CompletedTests.ContainsKey(key))
            return null;

        if (!Assignments.TryGetValue(key, out var assignment))
            return null;

        const string sql = """
            SELECT sr.SampleReceiptID,
                   tr.TestRequestID,
                   tt.TestTypeID,
                   p.PatientID,
                   COALESCE(sr.Barcode, '') AS Barcode,
                   LTRIM(RTRIM(CONCAT(u.Surname, ', ', u.Name))) AS PatientName,
                   COALESCE(u.IDNumber, '') AS PatientNumber,
                   COALESCE(tt.TestName, '') AS TestName,
                   COALESCE(tt.Units, '') AS Units,
                   tt.NormalRangeMin,
                   tt.NormalRangeMax,
                   COALESCE(tt.TurnAroundMinutes, 0) AS TurnAroundMinutes,
                   tr.RequestDate,
                   sr.DateTimeReceived,
                   COALESCE(tr.Urgency, '') AS Urgency,
                   COALESCE(tr.Status, '') AS RequestStatus
            FROM dbo.SampleReceipt sr
            INNER JOIN dbo.TestRequest tr
                ON tr.TestRequestID = sr.TestRequestID
            INNER JOIN dbo.Patient p
                ON p.PatientID = tr.PatientID
            INNER JOIN dbo.[User] u
                ON u.UserID = p.UserID
            INNER JOIN dbo.TestType tt
                ON tt.TestTypeID = @TestTypeID
            WHERE sr.SampleReceiptID = @SampleReceiptID
              AND tr.TestRequestID = @TestRequestID;
            """;

        await using var connection = new SqlConnection(_connectionString);

        var row = await connection.QuerySingleOrDefaultAsync<CaptureTestRow>(
            new CommandDefinition(
                sql,
                new
                {
                    SampleReceiptID = sampleReceiptId,
                    TestTypeID = testTypeId,
                    TestRequestID = assignment.TestRequestID
                },
                cancellationToken: cancellationToken));

        if (row == null || Rejections.ContainsKey(sampleReceiptId))
            return null;

        return new CaptureResultsViewModel
        {
            SampleReceiptID = row.SampleReceiptID,
            TestRequestID = row.TestRequestID,
            TestTypeID = row.TestTypeID,
            RequestNumber = $"REQ-{row.TestRequestID}",
            RegistrationNumber = row.PatientNumber,
            PatientName = row.PatientName,
            Barcode = row.Barcode,
            TestName = row.TestName,
            Units = row.Units,
            NormalRangeMin = row.NormalRangeMin,
            NormalRangeMax = row.NormalRangeMax,
            Status = "Awaiting Result",
            IsCompleted = false
        };
    }

    public async Task<(bool Success, string Message, int TestResultID)>
        SaveTestResultAsync(
            int sampleReceiptId,
            int testTypeId,
            string result,
            string technicianNotes,
            CancellationToken cancellationToken = default)
    {
        if (sampleReceiptId <= 0 || testTypeId <= 0)
            return (false, "Select a valid assigned test.", 0);

        if (string.IsNullOrWhiteSpace(result))
            return (false, "Enter a result value before saving.", 0);

        string key = $"{sampleReceiptId}:{testTypeId}";

        if (!Assignments.TryGetValue(key, out var assignment))
            return (false, "This test is not currently assigned.", 0);

        if (CompletedTests.ContainsKey(key))
            return (false, "This test has already been completed.", 0);

        if (Rejections.ContainsKey(sampleReceiptId))
            return (false, "This sample has been rejected.", 0);

        if (!decimal.TryParse(
                result,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal numericResult))
        {
            return (false, "Enter a valid numeric result.", 0);
        }

        const string rangeSql = """
            SELECT NormalRangeMin, NormalRangeMax
            FROM dbo.TestType
            WHERE TestTypeID = @TestTypeID;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var range = await connection.QuerySingleOrDefaultAsync<TestRangeRow>(
            new CommandDefinition(
                rangeSql,
                new { TestTypeID = testTypeId },
                cancellationToken: cancellationToken));

        if (range == null)
            return (false, "The selected test type could not be found.", 0);

        bool isLow = range.NormalRangeMin.HasValue &&
                     numericResult < range.NormalRangeMin.Value;

        bool isHigh = range.NormalRangeMax.HasValue &&
                      numericResult > range.NormalRangeMax.Value;

        bool isAbnormal = isLow || isHigh;

        string status = isLow
            ? "Abnormally Low"
            : isHigh
                ? "Abnormally High"
                : "Within Normal Range";

        const string insertSql = """
            INSERT INTO dbo.TestResult
                (Result, IsAbnormal, Status, CompletionDateTime, TestTypeID)
            OUTPUT INSERTED.TestResultID
            VALUES
                (@Result, @IsAbnormal, @Status, SYSDATETIME(), @TestTypeID);
            """;

        int testResultId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                insertSql,
                new
                {
                    Result = result.Trim(),
                    IsAbnormal = isAbnormal,
                    Status = status,
                    TestTypeID = testTypeId
                },
                cancellationToken: cancellationToken));

        CompletedTests[key] = new CompletedTest(
            testResultId,
            sampleReceiptId,
            assignment.TestRequestID,
            testTypeId,
            assignment.TestName,
            result.Trim(),
            status,
            DateTime.Now);

        // The current schema has no technician-notes column.
        _ = technicianNotes;

        return (
            true,
            $"Result saved for {assignment.TestName}: {status}. " +
            "Completion tracking and request association are currently in memory; " +
            "technician notes are not persisted by the existing schema.",
            testResultId);
    }

    public bool IsTestCompleted(int sampleReceiptId, int testTypeId)
    {
        return CompletedTests.ContainsKey($"{sampleReceiptId}:{testTypeId}");
    }

    public bool IsTestResultLinkedToRequest(
        int testResultId,
        int testRequestId)
    {
        return CompletedTests.Values.Any(
            x => x.TestResultID == testResultId &&
                 x.TestRequestID == testRequestId);
    }

    public async Task<IReadOnlyList<VerificationQueueItemViewModel>>
        GetVerificationQueueAsync(
            CancellationToken cancellationToken = default)
    {
        var completedTests = CompletedTests.Values
            .OrderByDescending(x => x.CompletionDateTime)
            .ToList();

        if (completedTests.Count == 0)
            return Array.Empty<VerificationQueueItemViewModel>();

        const string sql = """
            SELECT
                tr.TestRequestID,
                sr.SampleReceiptID,
                tt.TestTypeID,
                p.PatientID,
                COALESCE(sr.Barcode, '') AS Barcode,
                LTRIM(RTRIM(CONCAT(u.Surname, ', ', u.Name))) AS PatientName,
                COALESCE(u.IDNumber, '') AS PatientNumber,
                COALESCE(tt.TestName, '') AS TestName,
                COALESCE(tt.Units, '') AS Units,
                tt.NormalRangeMin,
                tt.NormalRangeMax,
                COALESCE(tr.Urgency, '') AS Urgency
            FROM dbo.SampleReceipt sr
            INNER JOIN dbo.TestRequest tr
                ON tr.TestRequestID = sr.TestRequestID
            INNER JOIN dbo.Patient p
                ON p.PatientID = tr.PatientID
            INNER JOIN dbo.[User] u
                ON u.UserID = p.UserID
            INNER JOIN dbo.TestType tt
                ON tt.TestTypeID = @TestTypeID
            WHERE sr.SampleReceiptID = @SampleReceiptID
              AND tr.TestRequestID = @TestRequestID;
            """;

        await using var connection = new SqlConnection(_connectionString);
        var items = new List<VerificationQueueItemViewModel>();

        foreach (var completed in completedTests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var patient = await connection.QuerySingleOrDefaultAsync<
                VerificationQueueItemViewModel>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        completed.SampleReceiptID,
                        completed.TestRequestID,
                        completed.TestTypeID
                    },
                    cancellationToken: cancellationToken));

            if (patient == null)
                continue;

            patient.TestResultID = completed.TestResultID;
            patient.Result = completed.Result;
            patient.ResultStatus = completed.Status;
            patient.IsAbnormal = !completed.Status.Equals(
                "Within Normal Range",
                StringComparison.OrdinalIgnoreCase);
            patient.CompletionDateTime = completed.CompletionDateTime;
            patient.RequestNumber = $"REQ-{completed.TestRequestID}";
            patient.CapturedBy = "Unknown";

            items.Add(patient);
        }

        return items;
    }

    public async Task<VerificationQueueItemViewModel?>
        GetVerificationResultAsync(
            int testResultId,
            CancellationToken cancellationToken = default)
    {
        if (testResultId <= 0)
            return null;

        var completed = CompletedTests.Values.FirstOrDefault(
            x => x.TestResultID == testResultId);

        if (completed == null)
            return null;

        const string sql = """
            SELECT
                tr.TestRequestID,
                sr.SampleReceiptID,
                tt.TestTypeID,
                p.PatientID,
                COALESCE(sr.Barcode, '') AS Barcode,
                LTRIM(RTRIM(CONCAT(u.Surname, ', ', u.Name))) AS PatientName,
                COALESCE(u.IDNumber, '') AS PatientNumber,
                COALESCE(tt.TestName, '') AS TestName,
                COALESCE(tt.Units, '') AS Units,
                tt.NormalRangeMin,
                tt.NormalRangeMax,
                COALESCE(tr.Urgency, '') AS Urgency
            FROM dbo.SampleReceipt sr
            INNER JOIN dbo.TestRequest tr
                ON tr.TestRequestID = sr.TestRequestID
            INNER JOIN dbo.Patient p
                ON p.PatientID = tr.PatientID
            INNER JOIN dbo.[User] u
                ON u.UserID = p.UserID
            INNER JOIN dbo.TestType tt
                ON tt.TestTypeID = @TestTypeID
            WHERE sr.SampleReceiptID = @SampleReceiptID
              AND tr.TestRequestID = @TestRequestID;
            """;

        await using var connection = new SqlConnection(_connectionString);

        var item = await connection.QuerySingleOrDefaultAsync<
            VerificationQueueItemViewModel>(
            new CommandDefinition(
                sql,
                new
                {
                    completed.SampleReceiptID,
                    completed.TestRequestID,
                    completed.TestTypeID
                },
                cancellationToken: cancellationToken));

        if (item == null)
            return null;

        item.TestResultID = completed.TestResultID;
        item.Result = completed.Result;
        item.ResultStatus = completed.Status;
        item.IsAbnormal = !completed.Status.Equals(
            "Within Normal Range",
            StringComparison.OrdinalIgnoreCase);
        item.CompletionDateTime = completed.CompletionDateTime;
        item.RequestNumber = $"REQ-{completed.TestRequestID}";
        item.CapturedBy = "Unknown";

        return item;
    }

    public async Task<(bool Success, string Message)> SaveVerificationDecisionAsync(
        int testResultId,
        int labTechnicianId,
        string decision,
        CancellationToken cancellationToken = default)
    {
        if (testResultId <= 0)
            return (false, "A valid test result is required.");

        if (labTechnicianId <= 0)
            return (false, "A valid lab technician must be identified before verification.");

        if (string.IsNullOrWhiteSpace(decision))
            return (false, "Select a verification decision.");

        var normalizedDecision = decision.Trim();

        if (!normalizedDecision.Equals("Approved", StringComparison.OrdinalIgnoreCase)
            && !normalizedDecision.Equals("Returned", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "The decision must be Approved or Returned.");
        }

        const string sql = """
            SET XACT_ABORT ON;

            BEGIN TRY
                BEGIN TRANSACTION;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.LabTechnician WITH (UPDLOCK, HOLDLOCK)
                    WHERE LabTechnicianID = @LabTechnicianID
                )
                BEGIN
                    THROW 50001, 'The selected lab technician does not exist.', 1;
                END;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.TestResult WITH (UPDLOCK, HOLDLOCK)
                    WHERE TestResultID = @TestResultID
                )
                BEGIN
                    THROW 50002, 'The selected test result does not exist.', 1;
                END;

                INSERT INTO dbo.ResultVerification
                    (VerificationDateTime, Decision, TestResultID, LabTechnicianID)
                VALUES
                    (SYSDATETIME(), @Decision, @TestResultID, @LabTechnicianID);

                COMMIT TRANSACTION;
            END TRY
            BEGIN CATCH
                IF @@TRANCOUNT > 0
                    ROLLBACK TRANSACTION;

                THROW;
            END CATCH;
            """;

        try
        {
            await using var connection = new SqlConnection(_connectionString);

            await connection.ExecuteAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TestResultID = testResultId,
                        LabTechnicianID = labTechnicianId,
                        Decision = normalizedDecision
                    },
                    cancellationToken: cancellationToken));

            return (true, $"Verification decision '{normalizedDecision}' was saved.");
        }
        catch (SqlException ex) when (ex.Number == 50001)
        {
            return (false, "The lab technician record was not found. No decision was saved.");
        }
        catch (SqlException ex) when (ex.Number == 50002)
        {
            return (false, "The test result was not found. No decision was saved.");
        }
        catch (Exception)
        {
            return (false, "The verification decision could not be saved.");
        }
    }
}