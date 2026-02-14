using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace SmartAttendance.Application.DTOs.Attendance
{
    public class JoinSessionDto
    {
        public int SessionId { get; set; }
        public string QrContent { get; set; } // İçinde Süre Bilgisi olan Dinamik QR

       
        // Öğrencinin o anki konumu
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // Güvenlik için
        public string DeviceId { get; set; }
    }

    public class JoinSessionResponseDto
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public string CourseName { get; set; }
    }
}