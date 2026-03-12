using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

namespace SmartAttendance.Application.Interfaces
{
    public interface IFaceRecognitionService
    {
        // Sınıfın fotoğrafını alır, içindeki yüzleri bulur ve veritabanındaki öğrencilerle eşleşenlerin ID'lerini döner
        Task<List<int>> IdentifyStudentsInCrowdAsync(int sessionId, IFormFile frame);
    }
}