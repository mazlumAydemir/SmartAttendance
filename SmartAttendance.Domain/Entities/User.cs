
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        // --- ÖĞRENCİ PROFİL FOTOĞRAFI ---
        // Öğrencinin sisteme yüklediği ve yüz tanımanın bakacağı referans fotoğraf
        public string? ProfilePictureUrl { get; set; }
        public UserRole Role { get; set; }

        // Güvenlik
       
        public string? FaceEncoding { get; set; }

        // İlişkiler (Navigation Properties)
        public ICollection<CourseEnrollment> Enrollments { get; set; }
        public ICollection<Course> GivenCourses { get; set; } // Hoca ise verdiği dersler
        public ICollection<StudentExcuse> Excuses { get; set; }

        // User.cs içine ekle:
        public string? FcmToken { get; set; } // Soru işareti (?) boş olabileceğini belirtir
    }
}