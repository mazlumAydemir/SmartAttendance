using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using SmartAttendance.Domain.Entities;
using SmartAttendance.Domain.Common;

namespace SmartAttendance.Infrastructure.Persistence
{
    public class SmartAttendanceDbContext : DbContext
    {
        public SmartAttendanceDbContext(DbContextOptions<SmartAttendanceDbContext> options)
            : base(options)
        {
        }

        // Veritabanı Tablolarımız
        public DbSet<User> Users { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<ClassLocation> ClassLocations { get; set; }
        public DbSet<CourseEnrollment> CourseEnrollments { get; set; }
        public DbSet<CourseSchedule> CourseSchedules { get; set; }
        public DbSet<AttendanceSession> AttendanceSessions { get; set; }
        public DbSet<SessionCourseLink> SessionCourseLinks { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
        public DbSet<StudentExcuse> StudentExcuses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 1. CourseEnrollment (Öğrenci - Ders)
            modelBuilder.Entity<CourseEnrollment>()
                .HasOne(ce => ce.Student)
                .WithMany(u => u.Enrollments)
                .HasForeignKey(ce => ce.StudentId)
                .OnDelete(DeleteBehavior.Restrict); // Öğrenci silinirse kayıt kalsın (veya hata versin)

            modelBuilder.Entity<CourseEnrollment>()
                .HasOne(ce => ce.Course)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(ce => ce.CourseId)
                .OnDelete(DeleteBehavior.Restrict); // Ders silinirse hata versin

            // 2. SessionCourseLink (HATAYI ÇÖZEN KISIM BURASI)
            // Hem Ders hem Oturum silindiğinde burası karışıyordu. Restrict ile çözdük.
            modelBuilder.Entity<SessionCourseLink>()
                .HasOne(scl => scl.AttendanceSession)
                .WithMany(s => s.RelatedCourses)
                .HasForeignKey(scl => scl.AttendanceSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SessionCourseLink>()
                .HasOne(scl => scl.Course)
                .WithMany()
                .HasForeignKey(scl => scl.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            // 3. AttendanceRecord (Yoklama Kaydı)
            modelBuilder.Entity<AttendanceRecord>()
                .HasOne(ar => ar.Student)
                .WithMany()
                .HasForeignKey(ar => ar.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AttendanceRecord>()
                .HasOne(ar => ar.AttendanceSession)
                .WithMany()
                .HasForeignKey(ar => ar.AttendanceSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            // 4. StudentExcuse (Mazeret)
            modelBuilder.Entity<StudentExcuse>()
                .HasOne(se => se.Course)
                .WithMany()
                .HasForeignKey(se => se.CourseId)
                .IsRequired(false);

            // 5. CourseSchedule (Ders Programı)
            // Ders silinince programın silinmesi sorun yaratabilir, bunu da kısıtlayalım.
            modelBuilder.Entity<CourseSchedule>()
                .HasOne(cs => cs.Course)
                .WithMany(c => c.Schedules)
                .HasForeignKey(cs => cs.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            // 6. Global Query Filter (Soft Delete)
            modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
            modelBuilder.Entity<Course>().HasQueryFilter(c => !c.IsDeleted);

            base.OnModelCreating(modelBuilder);
        }
    }
}