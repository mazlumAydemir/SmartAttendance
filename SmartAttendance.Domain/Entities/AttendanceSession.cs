using SmartAttendance.Domain.Common;
using SmartAttendance.Domain.Enums;
using System;
using System.Collections.Generic;

namespace SmartAttendance.Domain.Entities
{
    public class AttendanceSession : BaseEntity
    {
        public string SessionCode { get; set; } // QR kodu için üretilen benzersiz kod

        public int InstructorId { get; set; }
        public User Instructor { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; } // Bitiş saati boş olabilir (ders bitene kadar)

        public bool IsActive { get; set; } = true;

        // Ayarlar
        public AttendanceMethod Method { get; set; } // QR, Konum, Yüz
        public bool RequireFaceVerification { get; set; }
        public bool RequireDeviceVerification { get; set; }
        public bool RequireLocationVerification { get; set; }

        // ==============================================================
        // HATAYI ÇÖZEN KISIM: BU ALANLAR NULLABLE (?) OLMALI
        // ==============================================================

        // Hoca yoklamayı başlattığı andaki konumu (Referans Konum)
        public double? SnapshotLatitude { get; set; }  // <-- double yerine double?
        public double? SnapshotLongitude { get; set; } // <-- double yerine double?

        // Hoca kaç metre yarıçapa izin verdi? (Örn: 50m)
        public int? SnapshotRadius { get; set; }       // <-- int yerine int?

        // ==============================================================

        // Navigation Property (Çoka-Çok ilişki için ara tabloya gider)
        public ICollection<SessionCourseLink> RelatedCourses { get; set; }

        // Yoklama Kayıtları (Öğrencilerin girişleri)
        public ICollection<AttendanceRecord> AttendanceRecords { get; set; }
    }
}