using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SmartAttendance.Application.DTOs.Auth;

namespace SmartAttendance.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> LoginAsync(LoginDto model);

        // Eğer burada "Task<string> RegisterAsync..." varsa SİL.
        // Çünkü AuthService.cs içinde bu metodu iptal ettik/sildik.
    }
}