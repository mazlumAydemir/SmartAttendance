using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.DTOs.Admin
{
    public class RegisterFaceDto
    {
        public int StudentId { get; set; }
        public IFormFile FaceImage { get; set; }
    }
}