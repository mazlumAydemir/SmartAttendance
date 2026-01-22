using SmartAttendance.Domain.Common;
using System;

namespace SmartAttendance.Domain.Entities
{
    public class AttendanceRecord : BaseEntity
    {
        public int AttendanceSessionId { get; set; }
        public int StudentId { get; set; }
        public DateTime CheckInTime { get; set; }

        public bool IsDeviceVerified { get; set; }
        public bool IsFaceVerified { get; set; } // Yüz doğrulaması yapıldı mı?

        // --- BU SATIR EKSİKSE HATA ALIRSIN ---
        public string? FaceSnapshotUrl { get; set; }
        // -------------------------------------

        public bool IsValid { get; set; }
        public string UsedDeviceId { get; set; }
        public double DistanceFromSessionCenter { get; set; }

        // İlişkiler (Navigation Properties)
        public AttendanceSession AttendanceSession { get; set; }
        public User Student { get; set; }
    }
}