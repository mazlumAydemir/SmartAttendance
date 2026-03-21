using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.DTOs.Admin
{
    public class StudentListDto
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string SchoolNumber { get; set; }
        public bool IsActive { get; set; }
        public string DepartmentName { get; set; }
        public string GradeLevel { get; set; } // Örn: "3. Sınıf"
        public string? ProfilePictureUrl { get; set; }
    }
}