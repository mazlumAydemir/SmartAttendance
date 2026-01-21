using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using SmartAttendance.Application.DTOs.Course;
using SmartAttendance.Application.Interfaces;
using SmartAttendance.Infrastructure.Persistence;

namespace SmartAttendance.Infrastructure.Services
{
    public class CourseService : ICourseService
    {
        private readonly SmartAttendanceDbContext _context;

        public CourseService(SmartAttendanceDbContext context)
        {
            _context = context;
        }

        public async Task<List<TimetableItemDto>> GetStudentTimetableAsync(int studentId)
        {
            // 1. Öğrencinin kayıtlı olduğu dersleri bul
            var studentEnrollments = await _context.CourseEnrollments
                .Where(e => e.StudentId == studentId)
                .Select(e => e.CourseId)
                .ToListAsync();

            // 2. Bu derslerin programlarını (Schedule) getir
            var schedules = await _context.CourseSchedules
                .Include(s => s.Course)
                .ThenInclude(c => c.Instructor)
                .Include(s => s.ClassLocation)
                .Where(s => studentEnrollments.Contains(s.CourseId))
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.StartTime)
                .ToListAsync();

            // 3. DTO'ya çevir
            return schedules.Select(s => new TimetableItemDto
            {
                CourseCode = s.Course.CourseCode,
                CourseName = s.Course.CourseName,
                Day = s.DayOfWeek.ToString(),
                TimeSlot = $"{s.StartTime:hh\\:mm}-{s.EndTime:hh\\:mm}",
                ClassRoom = s.ClassLocation.RoomName,
                InstructorName = s.Course.Instructor.FullName
            }).ToList();
        }

        public async Task<List<TimetableItemDto>> GetInstructorTimetableAsync(int instructorId)
        {
            var schedules = await _context.CourseSchedules
                .Include(s => s.Course)
                .ThenInclude(c => c.Instructor)
                .Include(s => s.ClassLocation)
                .Where(s => s.Course.InstructorId == instructorId)
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.StartTime)
                .ToListAsync();

            return schedules.Select(s => new TimetableItemDto
            {
                CourseCode = s.Course.CourseCode,
                CourseName = s.Course.CourseName,
                Day = s.DayOfWeek.ToString(),
                TimeSlot = $"{s.StartTime:hh\\:mm}-{s.EndTime:hh\\:mm}",
                ClassRoom = s.ClassLocation.RoomName,
                InstructorName = s.Course.Instructor.FullName
            }).ToList();
        }
    }
}