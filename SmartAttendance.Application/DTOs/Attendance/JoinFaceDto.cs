using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http; // Resim dosyası (IFormFile) için şart

namespace SmartAttendance.Application.DTOs.Attendance
{
    public class JoinFaceDto
    {
        public int SessionId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string DeviceId { get; set; }

        // Önemli: Telefondan gelen fotoğraf burada tutulur
        public IFormFile FaceImage { get; set; }
    }
}
