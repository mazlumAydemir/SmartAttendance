using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SmartAttendance.Domain.Common;

namespace SmartAttendance.Domain.Entities
{
    public class AttendanceRecord : BaseEntity
    {
        public int AttendanceSessionId { get; set; }
        public AttendanceSession AttendanceSession { get; set; }

        public int StudentId { get; set; }
        public User Student { get; set; }

        public DateTime CheckInTime { get; set; }

        // Kanıtlar
        public bool IsFaceVerified { get; set; }
        public bool IsDeviceVerified { get; set; }
        public double DistanceFromSessionCenter { get; set; }
        public string? UsedDeviceId { get; set; }

        // Sonuç
        public bool IsValid { get; set; }
        public string? InvalidReason { get; set; }
    }
}