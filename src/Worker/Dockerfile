FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/Worker/Worker.csproj     src/Worker/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Shared/Shared.csproj     src/Shared/
RUN dotnet restore src/Worker/Worker.csproj

COPY src/ src/
RUN dotnet publish src/Worker/Worker.csproj -c Release -o /app/publish --no-restore

# runtime:8.0 is smaller than aspnet:8.0 — Worker has no HTTP stack.
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
RUN adduser --disabled-password --no-create-home appuser
COPY --from=build /app/publish .
USER appuser
ENTRYPOINT ["dotnet", "Worker.dll"]