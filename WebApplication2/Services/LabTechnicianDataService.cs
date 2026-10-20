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

    public async Task<IReadOnlyList<LabEscalationViewModel>>
     GetActiveEscalationsAsync(
         CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            tr.TestRequestID,
            sr.SampleReceiptID,
            tt.TestTypeID,
            LTRIM(RTRIM(CONCAT(u.Surname, ', ', u.Name))) AS PatientName,
            COALESCE(tt.TestName, '') AS TestName,
            COALESCE(tr.Urgency, '') AS Urgency,
            COALESCE(sr.Barcode, '') AS Barcode,
            LTRIM(RTRIM(CONCAT(
                COALESCE(doctor.Surname, ''),
                CASE WHEN doctor.Surname IS NOT NULL THEN ', ' ELSE '' END,
                COALESCE(doctor.Name, '')
            ))) AS DoctorName,
            sr.DateTimeReceived,
            COALESCE(tt.TurnAroundMinutes, 0) AS TurnAroundMinutes,
            DATEDIFF(
                MINUTE,
                DATEADD(
                    MINUTE,
                    tt.TurnAroundMinutes,
                    sr.DateTimeReceived
                ),
                GETDATE()
            ) AS MinutesOverdue
        FROM dbo.SampleReceipt sr
        INNER JOIN dbo.TestRequest tr
            ON tr.TestRequestID = sr.TestRequestID
        INNER JOIN dbo.Patient p
            ON p.PatientID = tr.PatientID
        INNER JOIN dbo.[User] u
            ON u.UserID = p.UserID
        LEFT JOIN dbo.Doctor d
            ON d.DoctorID = tr.DoctorID
        LEFT JOIN dbo.[User] doctor
            ON doctor.UserID = d.UserID
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
        INNER JOIN dbo.TestType tt
            ON LTRIM(RTRIM(requested.value)) = LTRIM(RTRIM(tt.TestName))
        WHERE COALESCE(tt.TurnAroundMinutes, 0) > 0
          AND sr.DateTimeReceived IS NOT NULL
          AND DATEADD(
                MINUTE,
                tt.TurnAroundMinutes,
                sr.DateTimeReceived
              ) < GETDATE()
          AND COALESCE(tr.Status, '') NOT IN
              ('Cancelled', 'Canceled', 'Completed', 'Rejected')
        ORDER BY
            CASE
                WHEN UPPER(LTRIM(RTRIM(COALESCE(tr.Urgency, '')))) = 'STAT'
                    THEN 0
                WHEN UPPER(LTRIM(RTRIM(COALESCE(tr.Urgency, '')))) = 'URGENT'
                    THEN 1
                ELSE 2
            END,
            MinutesOverdue DESC;
        """;

        await using var connection = new SqlConnection(_connectionString);

        var rows = await connection.QueryAsync<LabEscalationViewModel>(
            new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));

        return rows.AsList();
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

        const string latestDecisionSql = """
            SELECT latest.Decision
            FROM
            (
                SELECT TOP (1) tres.TestResultID
                FROM dbo.TestResult tres
                WHERE tres.TestRequestID = @TestRequestID
                  AND tres.TestTypeID = @TestTypeID
                ORDER BY tres.CompletionDateTime DESC, tres.TestResultID DESC
            ) newest
            OUTER APPLY
            (
                SELECT TOP (1) rv.Decision
                FROM dbo.ResultVerification rv
                WHERE rv.TestResultID = newest.TestResultID
                ORDER BY rv.VerificationDateTime DESC, rv.ResultVerificationID DESC
            ) latest;
            """;

        var latestDecision = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                latestDecisionSql,
                new { TestRequestID = (int)row.TestRequestID, TestTypeID = testTypeId },
                cancellationToken: cancellationToken));

        bool isReturnedForRecapture = string.Equals(
            latestDecision, "Returned", StringComparison.OrdinalIgnoreCase);

        if (CompletedTests.ContainsKey(key) && !isReturnedForRecapture)
            return (false, "This test already has a saved result. Only results returned by verification can be captured again.");

        var assignment = new Assignment(
            sampleReceiptId, (int)row.TestRequestID, testTypeId, testName,
            DateTime.Now, (int)row.TurnAroundMinutes);

        if (isReturnedForRecapture)
        {
            Assignments[key] = assignment;
            return (true, $"'{testName}' was returned for recapture and is ready to be captured again.");
        }

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

        if (!Assignments.TryGetValue(key, out var assignment))
            return null;

        await using var connection = new SqlConnection(_connectionString);

        const string latestDecisionSql = """
            SELECT latest.Decision
            FROM
            (
                SELECT TOP (1) tres.TestResultID
                FROM dbo.TestResult tres
                WHERE tres.TestRequestID = @TestRequestID
                  AND tres.TestTypeID = @TestTypeID
                ORDER BY tres.CompletionDateTime DESC, tres.TestResultID DESC
            ) newest
            OUTER APPLY
            (
                SELECT TOP (1) rv.Decision
                FROM dbo.ResultVerification rv
                WHERE rv.TestResultID = newest.TestResultID
                ORDER BY rv.VerificationDateTime DESC, rv.ResultVerificationID DESC
            ) latest;
            """;

        var latestDecision = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                latestDecisionSql,
                new { TestRequestID = assignment.TestRequestID, TestTypeID = testTypeId },
                cancellationToken: cancellationToken));

        if (CompletedTests.ContainsKey(key) &&
            !string.Equals(latestDecision, "Returned", StringComparison.OrdinalIgnoreCase))
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

       // await using var connection = new SqlConnection(_connectionString);

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
        {
            const string latestDecisionSql = """
                SELECT latest.Decision
                FROM
                (
                    SELECT TOP (1) tres.TestResultID
                    FROM dbo.TestResult tres
                    WHERE tres.TestRequestID = @TestRequestID
                      AND tres.TestTypeID = @TestTypeID
                    ORDER BY tres.CompletionDateTime DESC, tres.TestResultID DESC
                ) newest
                OUTER APPLY
                (
                    SELECT TOP (1) rv.Decision
                    FROM dbo.ResultVerification rv
                    WHERE rv.TestResultID = newest.TestResultID
                    ORDER BY rv.VerificationDateTime DESC, rv.ResultVerificationID DESC
                ) latest;
                """;

            await using var checkConnection = new SqlConnection(_connectionString);
            var latestDecision = await checkConnection.QuerySingleOrDefaultAsync<string>(
                new CommandDefinition(
                    latestDecisionSql,
                    new { TestRequestID = assignment.TestRequestID, TestTypeID = testTypeId },
                    cancellationToken: cancellationToken));

            if (!string.Equals(latestDecision, "Returned", StringComparison.OrdinalIgnoreCase))
                return (false, "This test has already been completed. Only a returned result can be recaptured.", 0);
        }

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
    (
        Result,
        IsAbnormal,
        Status,
        CompletionDateTime,
        TestTypeID,
        TestRequestID,
        TechnicianNotes
    )
    OUTPUT INSERTED.TestResultID
    VALUES
    (
        @Result,
        @IsAbnormal,
        @Status,
        SYSDATETIME(),
        @TestTypeID,
        @TestRequestID,
        @TechnicianNotes
    );
    """;


        int testResultId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                insertSql,
                new
                {
                    Result = result.Trim(),
                    IsAbnormal = isAbnormal,
                    Status = status,
                    TestTypeID = testTypeId,
                    TestRequestID = assignment.TestRequestID,
                    TechnicianNotes = string.IsNullOrWhiteSpace(technicianNotes)
                    ? null
                    : technicianNotes.Trim()
                },
                cancellationToken: cancellationToken));

        bool wasRecapture = CompletedTests.ContainsKey(key);
        CompletedTests[key] = new CompletedTest(
            testResultId,
            sampleReceiptId,
            assignment.TestRequestID,
            testTypeId,
            assignment.TestName,
            result.Trim(),
            status,
            DateTime.Now);

        await RecordAuditEventAsync(
            testResultId,
            wasRecapture ? "Recaptured" : "Captured",
            details: wasRecapture
                ? $"Recaptured {assignment.TestName}; prior returned result retained in history."
                : $"Captured {assignment.TestName}; result status: {status}.",
            cancellationToken: cancellationToken);

        return (
            true,
            wasRecapture
                ? $"Recaptured result saved for {assignment.TestName}: {status}. It is ready for verification."
                : $"Result saved for {assignment.TestName}: {status}.",
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
            COALESCE(tr.Urgency, '') AS Urgency,
            COALESCE(tres.Result, '') AS Result,
            COALESCE(tres.Status, '') AS ResultStatus,
            COALESCE(tres.IsAbnormal, 0) AS IsAbnormal,
            tres.CompletionDateTime,
            tres.TestResultID,
            CONCAT('REQ-', tr.TestRequestID) AS RequestNumber,
            COALESCE(latest.Decision, '') AS LatestVerificationDecision,
            COALESCE(latest.Reason, '') AS LatestVerificationReason,
            latest.VerificationDateTime AS LatestVerificationDateTime,
            'Unknown' AS CapturedBy
        FROM dbo.TestResult tres
        INNER JOIN dbo.TestRequest tr
            ON tr.TestRequestID = tres.TestRequestID
        CROSS APPLY
        (
            SELECT TOP (1) sr1.SampleReceiptID, sr1.Barcode, sr1.DateTimeReceived
            FROM dbo.SampleReceipt sr1
            WHERE sr1.TestRequestID = tr.TestRequestID
            ORDER BY sr1.DateTimeReceived DESC, sr1.SampleReceiptID DESC
        ) sr
        INNER JOIN dbo.Patient p
            ON p.PatientID = tr.PatientID
        INNER JOIN dbo.[User] u
            ON u.UserID = p.UserID
        INNER JOIN dbo.TestType tt
            ON tt.TestTypeID = tres.TestTypeID
        OUTER APPLY
        (
            SELECT TOP (1)
                rv.Decision,
                rv.Reason,
                rv.VerificationDateTime
            FROM dbo.ResultVerification rv
            WHERE rv.TestResultID = tres.TestResultID
            ORDER BY rv.VerificationDateTime DESC,
                     rv.ResultVerificationID DESC
        ) latest
        WHERE tres.TestRequestID IS NOT NULL
        ORDER BY tres.CompletionDateTime DESC,
                 tres.TestResultID DESC;
        """;

        await using var connection = new SqlConnection(_connectionString);

        var items = (await connection.QueryAsync<VerificationQueueItemViewModel>(
            new CommandDefinition(
                sql,
                cancellationToken: cancellationToken)))
            .AsList();

        const string historySql = """
        SELECT
            rv.ResultVerificationID,
            rv.Decision,
            COALESCE(rv.Reason, '') AS Reason,
            rv.VerificationDateTime,
            COALESCE(
                NULLIF(
                    LTRIM(RTRIM(CONCAT(usr.Surname, ', ', usr.Name))),
                    ','
                ),
                'Unknown'
            ) AS TechnicianName,
            rv.TestResultID
        FROM dbo.ResultVerification rv
        INNER JOIN dbo.LabTechnician lt
            ON lt.LabTechnicianID = rv.LabTechnicianID
        LEFT JOIN dbo.[User] usr
            ON usr.UserID = lt.UserID
        ORDER BY rv.VerificationDateTime,
                 rv.ResultVerificationID;
        """;

        var historyRows = await connection.QueryAsync<
            VerificationHistoryItemViewModelWithResultId>(
            new CommandDefinition(
                historySql,
                cancellationToken: cancellationToken));

        var historiesByResult = historyRows
            .GroupBy(x => x.TestResultID)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x =>
                    new VerificationHistoryItemViewModel
                    {
                        ResultVerificationID = x.ResultVerificationID,
                        Decision = x.Decision ?? string.Empty,
                        Reason = x.Reason ?? string.Empty,
                        TechnicianName = x.TechnicianName ?? "Unknown",
                        VerificationDateTime = x.VerificationDateTime
                    }).ToList());

        foreach (var item in items)
        {
            if (historiesByResult.TryGetValue(
                item.TestResultID, out var history))
            {
                item.VerificationHistory = history;
                item.HasBeenReturnedForRecapture = history.Any(x =>
                    string.Equals(
                        x.Decision, "Returned",
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        x.Decision, "Rejected",
                        StringComparison.OrdinalIgnoreCase));
            }
        }

        return items;
    }

    private sealed class VerificationHistoryItemViewModelWithResultId
    {
        public int TestResultID { get; set; }
        public int ResultVerificationID { get; set; }
        public string? Decision { get; set; }
        public string? Reason { get; set; }
        public string? TechnicianName { get; set; }
        public DateTime? VerificationDateTime { get; set; }
    }




    public async Task<VerificationQueueItemViewModel?>
        GetVerificationResultAsync(
            int testResultId,
            CancellationToken cancellationToken = default)
    {
        if (testResultId <= 0)
            return null;

        const string sql = """
        SELECT
            tr.TestRequestID,
            sr.SampleReceiptID,
            tt.TestTypeID,
            p.PatientID,
            COALESCE(sr.Barcode, '') AS Barcode,
            LTRIM(RTRIM(CONCAT(u.Surname, ', ', u.Name)))
                AS PatientName,
            COALESCE(u.IDNumber, '') AS PatientNumber,
            COALESCE(tt.TestName, '') AS TestName,
            COALESCE(tt.Units, '') AS Units,
            tt.NormalRangeMin,
            tt.NormalRangeMax,
            COALESCE(tr.Urgency, '') AS Urgency,
            COALESCE(tres.Result, '') AS Result,
            COALESCE(tres.Status, '') AS ResultStatus,
            COALESCE(tres.IsAbnormal, 0) AS IsAbnormal,
            tres.CompletionDateTime,
            tres.TestResultID,
            CONCAT('REQ-', tr.TestRequestID) AS RequestNumber,
            'Unknown' AS CapturedBy
        FROM dbo.TestResult tres
        INNER JOIN dbo.TestRequest tr
            ON tr.TestRequestID = tres.TestRequestID
        CROSS APPLY
        (
            SELECT TOP (1) sr1.SampleReceiptID, sr1.Barcode, sr1.DateTimeReceived
            FROM dbo.SampleReceipt sr1
            WHERE sr1.TestRequestID = tr.TestRequestID
            ORDER BY sr1.DateTimeReceived DESC, sr1.SampleReceiptID DESC
        ) sr
        INNER JOIN dbo.Patient p
            ON p.PatientID = tr.PatientID
        INNER JOIN dbo.[User] u
            ON u.UserID = p.UserID
        INNER JOIN dbo.TestType tt
            ON tt.TestTypeID = tres.TestTypeID
        WHERE tres.TestResultID = @TestResultID;
        """;

        await using var connection =
            new SqlConnection(_connectionString);


        var result = await connection.QuerySingleOrDefaultAsync<
            VerificationQueueItemViewModel>(
            new CommandDefinition(
                sql,
                new { TestResultID = testResultId },
                cancellationToken: cancellationToken));

        if (result == null)
            return null;

        const string historySql = """
    SELECT
        rv.ResultVerificationID,
        rv.TestResultID,
        rv.Decision,
        COALESCE(rv.Reason, '') AS Reason,
        rv.VerificationDateTime,
        COALESCE(
            NULLIF(
                LTRIM(RTRIM(CONCAT(usr.Surname, ', ', usr.Name))),
                ','
            ),
            'Unknown'
        ) AS TechnicianName
    FROM dbo.ResultVerification rv
    INNER JOIN dbo.LabTechnician lt
        ON lt.LabTechnicianID = rv.LabTechnicianID
    LEFT JOIN dbo.[User] usr
        ON usr.UserID = lt.UserID
    WHERE rv.TestResultID = @TestResultID
    ORDER BY rv.VerificationDateTime,
             rv.ResultVerificationID;
    """;

        var history = await connection.QueryAsync<
            VerificationHistoryItemViewModelWithResultId>(
            new CommandDefinition(
                historySql,
                new { TestResultID = testResultId },
                cancellationToken: cancellationToken));

        result.VerificationHistory = history.Select(x =>
            new VerificationHistoryItemViewModel
            {
                ResultVerificationID = x.ResultVerificationID,
                Decision = x.Decision ?? string.Empty,
                Reason = x.Reason ?? string.Empty,
                TechnicianName = x.TechnicianName ?? "Unknown",
                VerificationDateTime = x.VerificationDateTime
            }).ToList();

        result.HasBeenReturnedForRecapture = result.VerificationHistory.Any(x =>
            string.Equals(x.Decision, "Returned",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Decision, "Rejected",
                StringComparison.OrdinalIgnoreCase));

        var latest = result.VerificationHistory.LastOrDefault();

        if (latest != null)
        {
            result.LatestVerificationDecision = latest.Decision;
            result.LatestVerificationReason = latest.Reason;
            result.LatestVerificationDateTime = latest.VerificationDateTime;
        }

        return result;

    }


    public async Task<(bool Success, string Message)> SaveVerificationDecisionAsync(
    int testResultId,
    int labTechnicianId,
    string decision,
    string? reason,
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
                (VerificationDateTime, Decision, TestResultID, LabTechnicianID, Reason)
               VALUES
                (SYSDATETIME(), @Decision, @TestResultID, @LabTechnicianID, @Reason);

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
                        Decision = normalizedDecision,
                        Reason = reason
                    },
                    cancellationToken: cancellationToken));

            await RecordAuditEventAsync(
                testResultId,
                normalizedDecision,
                labTechnicianId,
                string.IsNullOrWhiteSpace(reason)
                    ? $"Verification decision: {normalizedDecision}."
                    : $"Verification decision: {normalizedDecision}. Reason: {reason.Trim()}",
                cancellationToken);

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
        catch (SqlException ex)
        {
            Console.Error.WriteLine(
                $"SaveVerificationDecisionAsync SQL error {ex.Number}: {ex.Message}");

            return (false,
                $"Database error ({ex.Number}): {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"SaveVerificationDecisionAsync error: {ex}");

            return (false,
                "The verification decision could not be saved. Check application logs.");
        }
    }

    public async Task<int?> GetLabTechnicianIdForUserAsync(
    int userId,
    CancellationToken cancellationToken = default)
    {
        const string sql = @"
        SELECT TOP (1) LabTechnicianID
        FROM dbo.LabTechnician
        WHERE UserID = @UserID;";

        await using var connection = new SqlConnection(_connectionString);

        return await connection.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(
                sql,
                new { UserID = userId },
                cancellationToken: cancellationToken));
    }

    public async Task RecordAuditEventAsync(
    int testResultId,
    string eventType,
    int? labTechnicianId = null,
    string? details = null,
    CancellationToken cancellationToken = default)
    {
        if (testResultId <= 0)
            throw new ArgumentOutOfRangeException(nameof(testResultId));

        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException(
                "An audit event type is required.",
                nameof(eventType));

        const string sql = """
        INSERT INTO dbo.TestResultAuditHistory
        (
            TestResultID,
            EventType,
            LabTechnicianID,
            Details
        )
        VALUES
        (
            @TestResultID,
            @EventType,
            @LabTechnicianID,
            @Details
        );
        """;

        await using var connection = new SqlConnection(_connectionString);

        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    TestResultID = testResultId,
                    EventType = eventType.Trim(),
                    LabTechnicianID = labTechnicianId,
                    Details = string.IsNullOrWhiteSpace(details)
                        ? null
                        : details.Trim()
                },
                cancellationToken: cancellationToken));
    }
}