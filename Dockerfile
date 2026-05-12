# 1. Derleme Aşaması
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "SmartAttendance/SmartAttendance.WebApi.csproj"
RUN dotnet publish "SmartAttendance/SmartAttendance.WebApi.csproj" -c Release -r linux-x64 --self-contained false -o /app/publish

# 2. Çalışma Aşaması - UBUNTU 22.04 (JAMMY)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS final
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    libgdiplus \
    libopencv-dev \
    libfontconfig1 \
    libx11-6 \
    libicu-dev \
    libice6 \
    libsm6 \
    libxtst6 \
    libxrender1 \
    libgtk2.0-0 \
    libgl1 \
    tesseract-ocr \
    libtesseract-dev \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN mkdir -p /app/runtimes/linux-x64/native/ && \
    cp runtimes/linux-x64/native/libOpenCvSharpExtern.so /app/runtimes/linux-x64/native/ 2>/dev/null || true && \
    cp runtimes/linux-x64/native/libOpenCvSharpExtern.so . 2>/dev/null || true

# Modelleri ve Key'i kopyala
COPY SmartAttendance/AI_Model ./AI_Model
COPY SmartAttendance/firebase-key.json ./firebase-key.json

ENTRYPOINT ["dotnet", "SmartAttendance.WebApi.dll"]