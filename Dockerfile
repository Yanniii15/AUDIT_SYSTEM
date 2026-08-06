# ==========================================
# 1. Build Stage
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files for dependency restoration
COPY ["AuditCkDayo.sln", "./"]
COPY ["AuditCkDayo/AuditCkDayo.csproj", "AuditCkDayo/"]
COPY ["AuditCkDayo.Tests/AuditCkDayo.Tests.csproj", "AuditCkDayo.Tests/"]

# Restore NuGet packages
RUN dotnet restore

# Copy the entire source code
COPY . .

# Run tests before publishing to ensure production safety
WORKDIR "/src/AuditCkDayo.Tests"
RUN dotnet test -c Release

# Publish the web application to /app/publish
WORKDIR "/src/AuditCkDayo"
RUN dotnet publish "AuditCkDayo.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# 2. Runtime Stage
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Configure default ports for ASP.NET Core 9.0
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Copy published binaries from the build stage
COPY --from=build /app/publish .

# Secure storage directory for uploaded receipts
RUN mkdir -p /app/Audits/Receipt

# Entrypoint to launch the web server
ENTRYPOINT ["dotnet", "AuditCkDayo.dll"]
