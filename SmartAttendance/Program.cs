using Microsoft.EntityFrameworkCore;
using SmartAttendance.Infrastructure.Persistence;
using SmartAttendance.Infrastructure.Services;
using SmartAttendance.Application.Interfaces; // DÜZELTME: Bu satýr eksikti, eklendi.

var builder = WebApplication.CreateBuilder(args);

// 1. Veritabaný Baðlantýsý
builder.Services.AddDbContext<SmartAttendanceDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Servisleri Baðla (Dependency Injection)
// Artýk AuthService, IAuthService'i miras aldýðý için bu satýr hata vermez.
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// --- SEED DATA (Test Verileri) ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<SmartAttendanceDbContext>();

        // Veritabanýný oluþtur veya güncelle
        context.Database.Migrate();

        // Test kullanýcýlarýný ekle
        await DataSeeder.SeedAsync(context);
    }
    catch (Exception ex)
    {
        Console.WriteLine("Seed data yüklenirken hata oluþtu: " + ex.Message);
    }
}
// ---------------------------------

app.Run();