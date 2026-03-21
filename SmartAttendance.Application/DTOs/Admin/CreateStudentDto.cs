using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SmartAttendance.Application.DTOs.Admin
{
    public class CreateStudentDto
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string SchoolNumber { get; set; }
        public int DepartmentId { get; set; }

        // YENİ: React'tan gelecek olan resim dosyası
        public IFormFile? ProfileImage { get; set; }
    }
}