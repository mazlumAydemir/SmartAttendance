using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SmartAttendance.Domain.Common;

namespace SmartAttendance.Domain.Entities
{
    public class CourseSchedule : BaseEntity
    {
        public int CourseId { get; set; }
        public Course Course { get; set; }

        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        // Sınıf Konumuna Bağlantı
        public int ClassLocationId { get; set; }
        public ClassLocation ClassLocation { get; set; }
    }
}