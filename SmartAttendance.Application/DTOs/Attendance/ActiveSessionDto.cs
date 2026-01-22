using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using System.Collections.Generic;

namespace SmartAttendance.Application.DTOs.Attendance
{
    public class ActiveSessionDto
    {
        public int SessionId { get; set; }       // Kapatmak için lazım
        public string SessionCode { get; set; }  // Ekranda göstermek için
        public DateTime StartTime { get; set; }  // Ne zaman başladı?
        public string MethodName { get; set; }   // QR mı, Yüz mü?
        public List<string> CourseNames { get; set; } // "Matematik 101, Fizik 202"
    }
}