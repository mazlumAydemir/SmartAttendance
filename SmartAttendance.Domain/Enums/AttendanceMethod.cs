using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Domain.Enums
{
    public enum AttendanceMethod
    {
        QrCode = 1,
        Location = 2,
        CrowdScan = 3  // FaceScan yerine CrowdScan yazıyoruz
    }
}