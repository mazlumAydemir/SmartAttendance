using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.DTOs.Course
{
    public class EnrolledStudentDto
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string SchoolNumber { get; set; }
        public string Email { get; set; }
    }
}
