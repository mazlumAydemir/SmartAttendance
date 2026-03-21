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
        // --- YENİ EKLENEN ALANLAR ---
        // ==========================================================
        // Bu ders hangi bölümün dersi? (Örn: Bilgisayar Mühendisliği dersi)
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }
        // ==========================================================

        public bool IsAutoAttendanceEnabled { get; set; } = false;
        public AttendanceMethod DefaultMethod { get; set; } = AttendanceMethod.QrCode;
        public int DefaultDurationMinutes { get; set; } = 15;
        public int DefaultRadiusMeters { get; set; } = 100;

        public ICollection<CourseEnrollment> Enrollments { get; set; }
        public ICollection<CourseSchedule> Schedules { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}