using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.DTOs.Admin
{
    public class AdminDashboardStatsDto
    {
        public int TotalFaculties { get; set; }
        public int TotalDepartments { get; set; }
        public int TotalCourses { get; set; }
        public int TotalTeachers { get; set; }
        public int TotalStudents { get; set; }
        public int ActiveStudents { get; set; }
        public int InactiveStudents { get; set; }
    }
}