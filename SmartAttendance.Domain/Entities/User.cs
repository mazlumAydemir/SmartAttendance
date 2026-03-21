using System;
using System.Collections.Generic;
using SmartAttendance.Domain.Common;
using SmartAttendance.Domain.Enums;

namespace SmartAttendance.Domain.Entities
{
    public class User : BaseEntity
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string? SchoolNumber { get; set; }

        public string? ProfilePictureUrl { get; set; }
        public UserRole Role { get; set; }

        public string? FaceEncoding { get; set; }
        public string? FcmToken { get; set; }

        // ==========================================================
        // --- YENİ EKLENEN ALANLAR ---
        // ==========================================================
        // Öğrenci veya Hoca aktif mi? (Kaydını sildirmiş/dondurmuş olabilir)
        public bool IsActive { get; set; } = true;

        // Öğrenci veya Hoca hangi bölüme kayıtlı? (Adminlerin bölümü olmayabilir diye Nullable yaptık)
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }
        // ==========================================================

        // İlişkiler
        public ICollection<CourseEnrollment> Enrollments { get; set; }
        public ICollection<Course> GivenCourses { get; set; }
        public ICollection<StudentExcuse> Excuses { get; set; }
    }
}