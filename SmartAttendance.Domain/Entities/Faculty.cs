using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SmartAttendance.Domain.Common;
using System.Collections.Generic;

namespace SmartAttendance.Domain.Entities
{
    public class Faculty : BaseEntity
    {
        public string Name { get; set; } // Örn: Mühendislik Fakültesi

        // Bir fakültenin birden çok bölümü olur
        public ICollection<Department> Departments { get; set; }
    }
}