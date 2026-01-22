using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartAttendance.Application.DTOs.Attendance;

namespace SmartAttendance.Application.Interfaces
{
    public interface IAttendanceService
    {
        Task<SessionResponseDto> StartSessionAsync(CreateSessionDto model, int instructorId);

        // QR ile katılma
        Task<JoinSessionResponseDto> JoinSessionAsync(JoinSessionDto model, int studentId);

        // Konum ile katılma
        Task<JoinSessionResponseDto> JoinSessionByLocationAsync(JoinLocationDto model, int studentId);

        // --- BU SATIR EKSİKTİ, BUNU EKLE ---
        Task<JoinSessionResponseDto> JoinSessionByFaceAsync(JoinFaceDto model, int studentId);
        // Bu dosyanın en altına, } parantezinden önce ekle:
        //yoklamayı bitirme metodu
        

        Task<List<ActiveSessionDto>> GetActiveSessionsAsync(int instructorId);

        // YENİ 2: Seçilen oturumu sonlandır
        Task<bool> EndSessionAsync(int sessionId, int instructorId);
    }
}