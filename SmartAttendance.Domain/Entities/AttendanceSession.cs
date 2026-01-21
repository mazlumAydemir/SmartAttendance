using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SmartAttendance.Domain.Common;

namespace SmartAttendance.Domain.Entities
{
    public class AttendanceSession : BaseEntity
    {
        public string SessionCode { get; set; } // QR Data (GUID)
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsActive { get; set; } = true;

        public int InstructorId { get; set; }
        public User Instructor { get; set; }

        // Snapshot (Ders başladığındaki konumun kopyası)
        public double? SnapshotLatitude { get; set; }
        public double? SnapshotLongitude { get; set; }
        public int? SnapshotRadius { get; set; }

        // Kurallar
        public bool RequireFaceVerification { get; set; }
        public bool RequireLocationVerification { get; set; }
        public bool RequireDeviceVerification { get; set; }

        // İlişki: Bir oturum birden fazla dersi kapsayabilir
        public ICollection<SessionCourseLink> RelatedCourses { get; set; }
    }
}