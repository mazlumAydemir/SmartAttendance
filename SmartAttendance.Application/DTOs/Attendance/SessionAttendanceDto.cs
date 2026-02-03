using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace SmartAttendance.Application.DTOs.Attendance
{
    public class SessionAttendanceDto
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string SchoolNumber { get; set; }
        public string Status { get; set; }       // "Present", "Absent", "Excused" olarak dönecek
        public string Description { get; set; }  // Hoca notu
        public DateTime CheckInTime { get; set; }
    }
}