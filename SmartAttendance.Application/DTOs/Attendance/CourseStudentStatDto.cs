using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.DTOs.Attendance
{
    public class CourseStudentStatDto
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string SchoolNumber { get; set; }
        public int TotalSessions { get; set; }    // Toplam ders sayısı
        public int AttendedSessions { get; set; } // Öğrencinin katıldığı

        // Yüzdelik hesaplamayı burada property içinde yapabiliriz:
        public int AttendancePercentage => TotalSessions == 0 ? 0 : (int)((double)AttendedSessions / TotalSessions * 100);
    }
}