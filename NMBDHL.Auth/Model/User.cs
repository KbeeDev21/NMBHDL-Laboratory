using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NMBDHL.Auth.Model
{
    public class User
    {
        [Key]
        public int UserID { get; set; }
        [Required]
        public string FirstName { get; set; }
        [Required]
        public string Lastname { get; set; }

        [Required]
        public string Role { get; set; }
        [Required]
        public string IDNumber { get; set; }

        [Required]
        public string CellPhoneNumber { get; set; }
        [Required]
        [RegularExpression("^[a-z0-9]+@[a-zA-Z0-9]+\\.[a-zA-Z]{2,}$", ErrorMessage = "Example. johndoe06@gmail.com. Must " +
        "contain @ sign after local name and end with . followed by top-level domain ")]
        public string Email { get; set; }
        [Required]
        [DataType(DataType.Password)]
        [RegularExpression(@"^(?=.[A-Za-z])(?=.\d)[A-Za-z\d]{8,}$", ErrorMessage = "Password must contain atleast 8 letters and/or numbers")]
        public string Password { get; set; }
        [Required]
        [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessage = "Passwords do not match.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }
        [NotMapped]
        

        public string Allergies { get; set; }
    }
}
