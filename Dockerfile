# 1. Çalışma Ortamı (Sunucuda uygulamanın koşacağı yer)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# 2. SDK ile Derleme Aşaması (Kodların birleştirildiği yer)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Bütün klasörleri ve kodları Docker'ın içine kopyala
COPY . .

# Projeleri birleştir ve derle (WebApi klasörünü hedef alıyoruz)
RUN dotnet restore "SmartAttendance.WebApi/SmartAttendance.WebApi.csproj"
RUN dotnet publish "SmartAttendance.WebApi/SmartAttendance.WebApi.csproj" -c Release -o /app/publish

# 3. Son Aşama: Temiz ve Çalışmaya Hazır Hali
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Firebase anahtarını da kopyalıyoruz ki bildirimler çalışsın
COPY SmartAttendance.WebApi/firebase-key.json ./firebase-key.json
COPY SmartAttendance/AI_Model ./AI_Model
# Uygulamayı Başlat
ENTRYPOINT ["dotnet", "SmartAttendance.WebApi.dll"]