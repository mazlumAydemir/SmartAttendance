using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace SmartAttendance.Application.DTOs.Admin
{
    // Listeleme İçin
    public class ClassLocationListDto
    {
        public int Id { get; set; }
        public string RoomName { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int FixedRadiusMeters { get; set; }
    }

    // Yeni Ekleme İçin
    public class CreateClassLocationDto
    {
        public string RoomName { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int FixedRadiusMeters { get; set; }
    }
}