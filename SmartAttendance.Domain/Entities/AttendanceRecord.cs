using SmartAttendance.Domain.Common;
using SmartAttendance.Domain.Enums; // <--- ENUM'I TANIMASI İÇİN BU GEREKLİ

namespace SmartAttendance.Domain.Entities
{
    public class AttendanceRecord : BaseEntity
    {
        public int AttendanceSessionId { get; set; }
        public AttendanceSession AttendanceSession { get; set; } // Nav. Prop.

        public int StudentId { get; set; }
        public User Student { get; set; } // Nav. Prop.

        public DateTime CheckInTime { get; set; }

        // ==========================================================
        // --- EKSİK OLAN VE GERİ EKLENMESİ GEREKEN ALANLAR ---
        // ==========================================================

        // 1: Present (Var), 2: Absent (Yok), 3: Excused (İzinli)
        public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

        // Hoca notu (Örn: "Raporlu", "Dersten erken çıktı" vb.)
        public string? Description { get; set; }

        // ==========================================================

        public bool IsDeviceVerified { get; set; }
        public bool IsFaceVerified { get; set; }

        public string? FaceSnapshotUrl { get; set; }

        public bool IsValid { get; set; } = true;
        public string? UsedDeviceId { get; set; }
        public double DistanceFromSessionCenter { get; set; }
    }
}