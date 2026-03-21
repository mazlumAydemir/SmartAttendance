using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.DTOs.Admin
{
    public class CourseStudentSelectionDto
    {
        public int UserId { get; set; } // Öğrenci ID
        public string FullName { get; set; }
        public string SchoolNumber { get; set; }
        public bool IsEnrolled { get; set; } // Bu derse ZATEN kayıtlı mı? (Checkbox işaretli gelsin diye)
    }
}