using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.DTOs.Attendance
{
    public class PastSessionDto
    {
        public int SessionId { get; set; }
        public string CourseNames { get; set; } // "CMPE428, CMPE500"
        public string Method { get; set; }      // QR, Location vs.
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int AttendedCount { get; set; }  // O gün kaç kişi gelmiş?
        public int TotalStudents { get; set; }  // Toplam sınıf mevcudu kaç?
    }
}