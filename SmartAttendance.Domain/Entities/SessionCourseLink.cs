using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SmartAttendance.Domain.Common;

namespace SmartAttendance.Domain.Entities
{
    public class SessionCourseLink : BaseEntity
    {
        public int AttendanceSessionId { get; set; }
        public AttendanceSession AttendanceSession { get; set; }

        public int CourseId { get; set; }
        public Course Course { get; set; }
    }
}