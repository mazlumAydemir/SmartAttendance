using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections.Generic;

namespace SmartAttendance.Application.DTOs.Course
{
    public class TeacherCourseGroupDto
    {
        // Birleştirilen derslerin ID'lerini tutacak (Örn: [5, 7])
        public List<int> CourseIds { get; set; }

        // Ekranda görünecek birleşik kod (Örn: "EKON111 / CMPE325")
        public string CourseCode { get; set; }

        // Ekranda görünecek birleşik isim
        public string CourseName { get; set; }
    }
}