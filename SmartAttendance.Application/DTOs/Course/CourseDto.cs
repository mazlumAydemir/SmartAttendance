using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.DTOs.Course
{
    public class CourseDto
    {
        public int Id { get; set; }
        public string CourseCode { get; set; } // Örn: CMPE428
        public string CourseName { get; set; } // Örn: Software Engineering

        public string InstructorName { get; set; }
    }
}
