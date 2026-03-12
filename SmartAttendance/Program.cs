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

var builder = WebApplication.CreateBuilder(args);

// --- 1. SIGNALR VE YEREL CORS AYARI ---
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        policy =>
        {
            // Lokal React adresin (genelde 5173 veya 3000 olur)
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
});

// --- 2. YEREL VERÝTABANI BAÐLANTISI ---
builder.Services.AddDbContext<SmartAttendanceDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- 3. DEPENDENCY INJECTION (Baðýmlýlýklarýn Eklenmesi) ---
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IAttendanceService, SmartAttendance.Infrastructure.Services.AttendanceService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHostedService<AutoAttendanceWorker>();
builder.Services.AddScoped<IFaceRecognitionService, FaceRecognitionService>(); // Yapay Zeka Servisimiz

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

    // Swagger ekranýna Authorize (Token) butonu ekleme
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Lütfen token'ýnýzý þu formatta girin: Bearer {token}",
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

var app = builder.Build();

// --- 6. PIPELINE AYARLARI ---
// Localde geliþtirme yaparken Swagger her zaman açýk olsun
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowSpecificOrigins");

// Yerel testlerde sorun yaþamamak için HTTPS yönlendirmesini opsiyonel yapabilirsin
// app.UseHttpsRedirection(); 

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<AttendanceHub>("/attendanceHub");
app.MapControllers();

// --- 7. OTOMATÝK SEED (Lokal DB'yi doldurmak için) ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<SmartAttendanceDbContext>();

    // Lokal veritabaný yoksa oluþturur ve Migration'larý yapar
    context.Database.Migrate();

    // DataSeeder sýnýfýndaki baþlangýç verilerini ekler
    await DataSeeder.SeedAsync(context);
}

app.Run();