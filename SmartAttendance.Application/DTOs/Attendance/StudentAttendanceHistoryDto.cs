using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;

namespace SmartAttendance.Application.DTOs.Attendance
{
    public class StudentAttendanceHistoryDto
    {
        public int SessionId { get; set; }
        public DateTime StartTime { get; set; }
        public string Method { get; set; } // QR, Location, Face
        public string Status { get; set; } // Present, Absent, Excused
        public string Description { get; set; } // "Otomatik Giriş" veya "Hoca mazeret verdi" vb.
    }
}