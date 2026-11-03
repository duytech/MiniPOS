# API image. Build from the repo root: docker build -f src/Pos.Api/Dockerfile .
# The root build files (Directory.Build.props, .editorconfig, sonar.globalconfig) are copied so the image builds with
# the same analyzers and warnings-as-errors as CI.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Restore first, from the project files only, so the layer is cached until a dependency changes.
COPY Directory.Build.props .editorconfig sonar.globalconfig CodeMetricsConfig.txt ./
COPY src/Pos.Domain/Pos.Domain.csproj src/Pos.Domain/
COPY src/Pos.Application/Pos.Application.csproj src/Pos.Application/
COPY src/Pos.Infrastructure/Pos.Infrastructure.csproj src/Pos.Infrastructure/
COPY src/Pos.Api/Pos.Api.csproj src/Pos.Api/
RUN dotnet restore src/Pos.Api/Pos.Api.csproj

COPY src/ src/
RUN dotnet publish src/Pos.Api/Pos.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# The aspnet image ships a non-root "app" user.
USER app
ENTRYPOINT ["dotnet", "Pos.Api.dll"]
