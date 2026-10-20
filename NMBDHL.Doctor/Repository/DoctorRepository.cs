using NMBDHL.DataAccess.SQLDataBaseAccess;
using NMBDHL.Doctor.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NMBDHL.Doctor.Repository
{
    public class DoctorRepository : IDoctorRepository
    {
        private readonly ISQLDataBaseAccess _db;

        public DoctorRepository(ISQLDataBaseAccess db)
        {
            _db = db;
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
    }
}