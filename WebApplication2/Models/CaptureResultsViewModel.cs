namespace WebApplication2.Models
{
    public class CaptureResultsViewModel
    {
        public string RequestNumber { get; set; } = "REQ-2026-00124";
        public string PatientName { get; set; } = "Baloyi, Thabo";
        public int PatientAge { get; set; } = 42;

        public string Haemoglobin { get; set; } = "";
        public string Wcc { get; set; } = "";
        public string Platelets { get; set; } = "";

        public string HaemoglobinNotes { get; set; } = "";
        public string WccNotes { get; set; } = "";
        public string PlateletsNotes { get; set; } = "";

        public string HaemoglobinStatus { get; set; } = "Awaiting Result";
        public string WccStatus { get; set; } = "Awaiting Result";
        public string PlateletsStatus { get; set; } = "Awaiting Result";
    }
}