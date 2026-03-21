using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.DTOs.Admin
{
    public class InstructorLookupDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
    public class FacultyLookupDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class DepartmentLookupDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int FacultyId { get; set; } // Hangi fakülteye ait olduğunu bilmek için
    }
    public class ClassLocationLookupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } // RoomName (Örn: CMPE128)
    }
}