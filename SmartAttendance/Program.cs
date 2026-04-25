using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartAttendance.Application.Interfaces;
using SmartAttendance.Infrastructure.BackgroundServices;
using SmartAttendance.Infrastructure.Persistence;
using SmartAttendance.Infrastructure.Services;
using SmartAttendance.Infrastructure.Hubs;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;
using System;
using System.Threading.Tasks;

namespace SmartAttendance.WebApi
{
    public class Program
    {
        // Asenkron işlemler (await) olduğu için Main metodunu async Task yaptık
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // --- 1. SIGNALR VE YEREL CORS AYARI ---
            builder.Services.AddSignalR();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", corsBuilder =>
                {
                    corsBuilder.WithOrigins(
                            "http://localhost:5173",
                            "http://172.29.84.73:5173",
                            "https://smart-attendance-frontend-nine.vercel.app",
                            "https://delaine-ungrooved-yosef.ngrok-free.dev" // <--- NGROK LİNKİN BURADA
                           )
                           .AllowAnyMethod()
                           .AllowAnyHeader()
                           .AllowCredentials();
                });
            });

            // --- 2. YEREL VERİTABANI BAĞLANTISI ---
            builder.Services.AddDbContext<SmartAttendanceDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // --- 3. DEPENDENCY INJECTION (Bağımlılıkların Eklenmesi) ---
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<ICourseService, CourseService>();
            builder.Services.AddScoped<IAttendanceService, SmartAttendance.Infrastructure.Services.AttendanceService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddHostedService<AutoAttendanceWorker>();

            // Yapay Zeka Servisimiz Singleton (Ölümsüz)
            builder.Services.AddSingleton<IFaceRecognitionService, FaceRecognitionService>();

            builder.Services.AddScoped<IAdminService, AdminService>();

            // --- 4. JWT AYARLARI ---
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
                };
            });

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();

            // --- 5. SWAGGER VE AUTHORIZE BUTONU AYARLARI ---
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "SmartAttendance API", Version = "v1" });

                // Swagger ekranına Authorize (Token) butonu ekleme
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Lütfen token'ınızı şu formatta girin: Bearer {token}",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                            Scheme = "oauth2",
                            Name = "Bearer",
                            In = ParameterLocation.Header,
                        },
                        new List<string>()
                    }
                });
            });

            // ==========================================================
            // --- YENİ EKLENECEK FIREBASE BAŞLATMA KODU ---
            // ==========================================================
            var firebaseKeyPath = Path.Combine(Directory.GetCurrentDirectory(), "firebase-key.json");
            if (File.Exists(firebaseKeyPath) && FirebaseApp.DefaultInstance == null)
            {
                FirebaseApp.Create(new AppOptions()
                {
                    Credential = GoogleCredential.FromFile(firebaseKeyPath)
                });
                Console.WriteLine("Firebase başarıyla başlatıldı!");
            }
            else if (!File.Exists(firebaseKeyPath))
            {
                Console.WriteLine("DİKKAT: firebase-key.json dosyası bulunamadı! Bildirimler çalışmayacak.");
            }
            // ==========================================================

            var app = builder.Build();

            // --- 6. PIPELINE AYARLARI ---
            // Localde geliştirme yaparken Swagger her zaman açık olsun
            app.UseSwagger();
            app.UseSwaggerUI();

            // İŞTE BURASI DÜZELTİLDİ! Yukarıdaki "AllowAll" ismiyle aynı oldu.
            app.UseCors("AllowAll");

            app.UseAuthentication();

            // BU SATIRI EKLİYORUZ: Sunucudaki resimlerin dışarıdan okunabilmesini sağlar
            app.UseStaticFiles();
            app.UseAuthorization();

            app.MapHub<AttendanceHub>("/attendanceHub");
            app.MapControllers();

            // --- 7. OTOMATİK SEED (Lokal DB'yi doldurmak için) ---
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var context = services.GetRequiredService<SmartAttendanceDbContext>();

                // Lokal veritabanı yoksa oluşturur ve Migration'ları yapar
                context.Database.Migrate();

                // Yapay zeka servisini çağırıp DataSeeder'a yolluyoruz
                var faceService = services.GetRequiredService<IFaceRecognitionService>();
                await DataSeeder.SeedAsync(context, faceService);
            }

            app.Run();
        }
    }
}