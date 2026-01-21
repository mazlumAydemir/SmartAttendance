using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SmartAttendance.Domain.Common;

namespace SmartAttendance.Domain.Entities
{
    public class StudentExcuse : BaseEntity
    {
        public int StudentId { get; set; }
        public User Student { get; set; }

        // Null ise tüm derslerden izinli, dolu ise sadece o dersten.
        public int? CourseId { get; set; }
        public Course? Course { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public string ReasonTitle { get; set; }
        public string? Description { get; set; }
        public string? DocumentPath { get; set; }

        public bool IsApproved { get; set; } = false;
    }
}