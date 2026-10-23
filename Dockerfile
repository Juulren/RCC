# Frontend build; no API credentials are needed or accepted at build time.
FROM node:24-bookworm-slim AS web
WORKDIR /web
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY src/Rcc.Ai/Rcc.Ai.csproj src/Rcc.Ai/
COPY src/Rcc.Api/Rcc.Api.csproj src/Rcc.Api/
RUN dotnet restore src/Rcc.Api/Rcc.Api.csproj
COPY src/Rcc.Ai/ src/Rcc.Ai/
COPY src/Rcc.Api/ src/Rcc.Api/
COPY --from=web /web/dist/ src/Rcc.Api/wwwroot/
RUN dotnet publish src/Rcc.Api/Rcc.Api.csproj --configuration Release --no-restore --output /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
COPY --from=build --chown=app:app /publish/ ./
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Rcc.Api.dll"]
