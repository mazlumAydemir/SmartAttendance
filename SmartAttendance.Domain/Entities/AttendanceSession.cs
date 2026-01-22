using SmartAttendance.Domain.Common;
using SmartAttendance.Domain.Enums; // Enum'ı tanıması için şart
using System;
using System.Collections.Generic;

namespace SmartAttendance.Domain.Entities
{
    public class AttendanceSession : BaseEntity
    {
        public string SessionCode { get; set; }
        public int InstructorId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsActive { get; set; }

        // Seçilen Yöntem (QR, Location, Face)
        public AttendanceMethod Method { get; set; }

        // Güvenlik Ayarları
        public bool RequireFaceVerification { get; set; }
        public bool RequireDeviceVerification { get; set; }
        public bool RequireLocationVerification { get; set; }

        // Konum Snapshot (Oturum açıldığı anki merkez)
        public double? SnapshotLatitude { get; set; }
        public double? SnapshotLongitude { get; set; }
        public int? SnapshotRadius { get; set; }

        // İlişkiler
        public ICollection<SessionCourseLink> RelatedCourses { get; set; }
    }
}