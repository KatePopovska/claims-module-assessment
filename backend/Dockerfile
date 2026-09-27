# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props NuGet.Config ./
COPY src/ClaimsModule.Domain/ClaimsModule.Domain.csproj src/ClaimsModule.Domain/
COPY src/ClaimsModule.Application/ClaimsModule.Application.csproj src/ClaimsModule.Application/
COPY src/ClaimsModule.Infrastructure/ClaimsModule.Infrastructure.csproj src/ClaimsModule.Infrastructure/
COPY src/ClaimsModule.Persistence/ClaimsModule.Persistence.csproj src/ClaimsModule.Persistence/
COPY src/ClaimsModule.API/ClaimsModule.API.csproj src/ClaimsModule.API/
RUN dotnet restore src/ClaimsModule.API/ClaimsModule.API.csproj

COPY src/ src/
RUN dotnet publish src/ClaimsModule.API/ClaimsModule.API.csproj --configuration Release --no-restore --output /app/publish

FROM build AS migrations-build
RUN dotnet tool install --global dotnet-ef --version 9.0.20
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet ef migrations bundle \
      --project src/ClaimsModule.Persistence \
      --startup-project src/ClaimsModule.API \
      --configuration Release \
      --output /app/efbundle \
      --force

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS migrate
WORKDIR /app
COPY --from=migrations-build /app/efbundle ./efbundle
USER $APP_UID
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS api
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/uploads && chown $APP_UID /app/uploads
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ClaimsModule.API.dll"]
