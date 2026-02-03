using SmartAttendance.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.DTOs.Attendance
{
    public class ManualAttendanceDto
    {
        public int SessionId { get; set; }
        public int StudentId { get; set; }
        public AttendanceStatus Status { get; set; } // 1:Var, 2:Yok, 3:İzinli
        public string? Description { get; set; }     // "Raporlu" vb.
    }
}