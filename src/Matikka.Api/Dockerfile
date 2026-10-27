# Rakennetaan repositorion juuresta:  docker build -f src/Matikka.Api/Dockerfile .
# Sama kontti toimii Azure Container Appsissa ja tavallisella VPS:llä (esim. Hetzner).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/Matikka.Core/ src/Matikka.Core/
COPY src/Matikka.Api/ src/Matikka.Api/
RUN dotnet publish src/Matikka.Api/Matikka.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Matikka.Api.dll"]
