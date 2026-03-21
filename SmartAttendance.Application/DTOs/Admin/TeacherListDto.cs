using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.DTOs.Admin
{
    public class TeacherListDto
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string SchoolNumber { get; set; } // Sicil No olarak kullanacağız
        public bool IsActive { get; set; }
        public string DepartmentName { get; set; }
        public string FacultyName { get; set; }
        public int CourseCount { get; set; } // "2 Ders" yazısı için
    }
}