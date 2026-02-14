using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.DTOs.Attendance
{
    public class JoinLocationDto
    {
        public int SessionId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string DeviceId { get; set; }
    }
}