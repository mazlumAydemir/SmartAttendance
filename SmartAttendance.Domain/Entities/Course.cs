using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SmartAttendance.Domain.Common;

namespace SmartAttendance.Domain.Entities
{
    public class Course : BaseEntity
    {
        public string CourseCode { get; set; } // CMPE428
        public string CourseName { get; set; }

        public int InstructorId { get; set; }
        public User Instructor { get; set; }

        public ICollection<CourseEnrollment> Enrollments { get; set; }
        public ICollection<CourseSchedule> Schedules { get; set; }
    }
}