using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.DTOs.Course
{
    public class TimetableItemDto
    {
        public string CourseCode { get; set; }  // CMPE428
        public string CourseName { get; set; }  // Software Engineering
        public string Day { get; set; }         // Monday
        public string TimeSlot { get; set; }    // 08:30 - 09:20
        public string ClassRoom { get; set; }   // CMPE128
        public string InstructorName { get; set; } // Yıltan Bitirim
    }
}