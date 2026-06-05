# syntax=docker/dockerfile:1

FROM node:24-alpine AS frontend
WORKDIR /src/client
COPY client/package*.json ./
RUN npm ci
COPY client ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY server/NormalAssNote.Api.csproj server/
COPY server/src/NormalAssNote.Domain/NormalAssNote.Domain.csproj server/src/NormalAssNote.Domain/
COPY server/src/NormalAssNote.Application/NormalAssNote.Application.csproj server/src/NormalAssNote.Application/
COPY server/src/NormalAssNote.Infrastructure/NormalAssNote.Infrastructure.csproj server/src/NormalAssNote.Infrastructure/
RUN dotnet restore server/NormalAssNote.Api.csproj

COPY server ./server
COPY --from=frontend /src/client/dist ./server/wwwroot
RUN dotnet publish server/NormalAssNote.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:SkipFrontendBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_EnableDiagnostics=0

EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID

ENTRYPOINT ["dotnet", "NormalAssNote.Api.dll"]
