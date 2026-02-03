using SmartAttendance.Domain.Common;
using SmartAttendance.Domain.Enums;
using System.Collections.Generic;

namespace SmartAttendance.Domain.Entities
{
    public class Course : BaseEntity
    {
        public string CourseCode { get; set; }
        public string CourseName { get; set; }

        public int InstructorId { get; set; }
        public User Instructor { get; set; }

        // ==========================================================
        // --- EKSİK OLAN KISIMLAR (BUNLARI EKLEMEN LAZIM) ---
        // ==========================================================
        public bool IsAutoAttendanceEnabled { get; set; } = false; // Otomatik başlatma açık mı?
        public AttendanceMethod DefaultMethod { get; set; } = AttendanceMethod.QrCode; // Varsayılan Yöntem
        public int DefaultDurationMinutes { get; set; } = 15; // Kaç dakika açık kalacak?
        public int DefaultRadiusMeters { get; set; } = 100; // Konum ise yarıçap ne?
        // ==========================================================

        public ICollection<CourseEnrollment> Enrollments { get; set; }
        public ICollection<CourseSchedule> Schedules { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}