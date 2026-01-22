using System.Collections.Generic;
using System; // DateTime için gerekli
using SmartAttendance.Domain.Enums;

namespace SmartAttendance.Application.DTOs.Attendance
{
    public class CreateSessionDto
    {
        public List<int> CourseIds { get; set; }
        public AttendanceMethod Method { get; set; }
        public bool RequireFaceVerification { get; set; }
        public bool RequireDeviceVerification { get; set; } = true;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int RadiusMeters { get; set; } = 50;

        // YENİ EKLENEN ALAN: Tarih ve Saat Seçimi
        // Hoca isterse doldurur, istemezse boş bırakır (Boşsa "Şimdi" olur)
        public DateTime? StartTime { get; set; }
    }

    public class SessionResponseDto
    {
        public int SessionId { get; set; }
        public string SessionCode { get; set; }
        public string QrCodeContent { get; set; }
    }
}