using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.DTOs.Admin
{
    public class StudentStatsDto
    {
        public int TotalStudents { get; set; }
        public int ActiveStudents { get; set; }
    }
}