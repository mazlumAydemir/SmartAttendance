using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.DTOs.Attendance
{
    public class StudentActiveSessionDto
    {
        public int SessionId { get; set; }        // Öğrenci bunu seçip POST atacak
        public string CourseCode { get; set; }    // Örn: CMPE428
        public string CourseName { get; set; }    // Örn: Software Engineering
        public string InstructorName { get; set; } // Örn: Yıltan Hoca
        public DateTime StartTime { get; set; }
        public int RemainingTimeMinutes { get; set; } // Kalan süre (Opsiyonel görsel için)
    }
}