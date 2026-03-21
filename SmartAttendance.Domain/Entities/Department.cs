using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SmartAttendance.Domain.Common;
using System.Collections.Generic;

namespace SmartAttendance.Domain.Entities
{
    public class Department : BaseEntity
    {
        public string Name { get; set; } // Örn: Bilgisayar Mühendisliği

        // Hangi Fakülteye Bağlı?
        public int FacultyId { get; set; }
        public Faculty Faculty { get; set; }

        // Bu bölümdeki Öğrenciler ve Hocalar
        public ICollection<User> Users { get; set; }

        // Bu bölüme ait Dersler
        public ICollection<Course> Courses { get; set; }
    }
}