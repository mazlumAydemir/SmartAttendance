using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SmartAttendance.Domain.Common;

namespace SmartAttendance.Domain.Entities
{
    public class ClassLocation : BaseEntity
    {
        public string RoomName { get; set; } // Örn: CMPE128
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int FixedRadiusMeters { get; set; } = 50;
    }
}