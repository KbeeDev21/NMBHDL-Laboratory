using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NMBDHL.DataAccess.SQLDataBaseAccess;
using NMBDHL.Doctor.Model;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace NMBDHL.Doctor.Repository
{
    public class DoctorRepository : IDoctorRepository
    {
        private readonly ISQLDataBaseAccess _db;
        private readonly string _connectionString;

        // IConfiguration is added here to read from appsettings.json
        public DoctorRepository(ISQLDataBaseAccess db, IConfiguration config)
        {
            _db = db;
            _connectionString = config.GetConnectionString("DefaultConnection");
        }

        public async Task<IEnumerable<AbnormalAlertModel>> GetAbnormalAlertsAsync(int doctorId)
        {
            return await _db.GetData<AbnormalAlertModel, dynamic>(
                "sp_Doctor_GetAbnormalAlerts",
                new { DoctorID = doctorId });
        }

        public async Task<IEnumerable<RecentRequestModel>> GetRecentRequestsAsync(int doctorId)
        {
            return await _db.GetData<RecentRequestModel, dynamic>(
                "sp_Doctor_GetRecentRequests",
                new { DoctorID = doctorId });
        }

        public async Task<IEnumerable<PatientRecordModel>> GetManagePatientsAsync(int doctorId)
        {
            return await _db.GetData<PatientRecordModel, dynamic>(
                "sp_Doctor_GetManagePatients",
                new { DoctorID = doctorId });
        }

        public async Task<IEnumerable<PatientDropdownModel>> GetPatientsDropdownAsync()
        {
            return await _db.GetData<PatientDropdownModel, dynamic>(
                "sp_Doctor_GetPatientsDropdown",
                new { });
        }

        public async Task<IEnumerable<string>> GetTestPanelsAsync()
        {
            return await _db.GetData<string, dynamic>(
                "sp_Doctor_GetTestPanels",
                new { });
        }

        public async Task<IEnumerable<TrackProgressModel>> GetTrackProgressAsync(int doctorId)
        {
            return await _db.GetData<TrackProgressModel, dynamic>(
                "sp_Doctor_GetTrackProgress",
                new { DoctorID = doctorId });
        }

        public async Task<bool> CreateTestRequestAsync(int patientId, int doctorId, string panels, string urgency, string notes)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var parameters = new
                {
                    PatientID = patientId,
                    DoctorID = doctorId,
                    Type = panels,
                    Urgency = urgency,
                    ClinicalNotes = notes
                };

                int rowsAffected = await connection.ExecuteAsync(
                    "dbo.sp_Doctor_CreateTestRequest",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                return rowsAffected > 0;
            }
        }
    }
}