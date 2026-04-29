# ============================
# BUILD STAGE
# ============================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ARG NUGET_AUTH_TOKEN
ENV NUGET_AUTH_TOKEN=$NUGET_AUTH_TOKEN

# Copy solution and project files
COPY ZawatSys.MicroService.Communication.slnx .
COPY nuget.config .
COPY src/ZawatSys.MicroService.Communication.Api/*.csproj src/ZawatSys.MicroService.Communication.Api/
COPY src/ZawatSys.MicroService.Communication.Application/*.csproj src/ZawatSys.MicroService.Communication.Application/
COPY src/ZawatSys.MicroService.Communication.Infrastructure/*.csproj src/ZawatSys.MicroService.Communication.Infrastructure/

# Restore dependencies
RUN dotnet restore src/ZawatSys.MicroService.Communication.Api/ZawatSys.MicroService.Communication.Api.csproj

# Copy everything else
COPY . .

# Build and publish
RUN dotnet publish src/ZawatSys.MicroService.Communication.Api/ZawatSys.MicroService.Communication.Api.csproj -c Release -o /app/publish

# ============================
# RUNTIME STAGE
# ============================
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ZawatSys.MicroService.Communication.Api.dll"]
