using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;

namespace SmartAttendance.Application.DTOs.Admin
{
    public class CreateCourseScheduleDto
    {
        public int CourseId { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int ClassLocationId { get; set; }
    }
}