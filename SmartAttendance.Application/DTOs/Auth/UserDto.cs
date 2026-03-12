using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.DTOs.Auth
{
    public class UserDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } //
        public string Email { get; set; } //
        public string? SchoolNumber { get; set; } //
        public string Role { get; set; } //
        public string? FcmToken { get; set; } //
    }
}
