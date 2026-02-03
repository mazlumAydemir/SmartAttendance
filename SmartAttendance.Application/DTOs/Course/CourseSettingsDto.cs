using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartAttendance.Domain.Enums;

namespace SmartAttendance.Application.DTOs.Course
{
    public class CourseSettingsDto
    {
        public int CourseId { get; set; }
        public bool IsAutoAttendanceEnabled { get; set; }
        public AttendanceMethod DefaultMethod { get; set; } // 1:QR, 2:Location, 3:Face
        public int DefaultDurationMinutes { get; set; }
        public int DefaultRadiusMeters { get; set; }
    }
}