# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (cached unless the project file or global.json changes).
COPY global.json ./
COPY src/LNDocumentProcessor.Api/*.csproj ./src/LNDocumentProcessor.Api/
RUN dotnet restore ./src/LNDocumentProcessor.Api/LNDocumentProcessor.Api.csproj

# Copy the rest of the source and publish a release build.
COPY src/ ./src/
RUN dotnet publish ./src/LNDocumentProcessor.Api/LNDocumentProcessor.Api.csproj \
    -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Listen on 8080 (no HTTPS in-container; terminate TLS at the ingress).
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "LNDocumentProcessor.Api.dll"]
