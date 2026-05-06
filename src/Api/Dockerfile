FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first so restore layer is cached independently of source changes.
# Api references Worker, Infrastructure, and Shared.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Api/Api.csproj                        Api/
COPY src/Worker/Worker.csproj                  Worker/
COPY src/Infrastructure/Infrastructure.csproj  Infrastructure/
COPY src/Shared/Shared.csproj                  Shared/
RUN dotnet restore Api/Api.csproj

COPY src/Api/           Api/
COPY src/Worker/        Worker/
COPY src/Infrastructure/ Infrastructure/
COPY src/Shared/        Shared/
RUN dotnet publish Api/Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN adduser --disabled-password --no-create-home appuser
COPY --from=build /app/publish .
USER appuser
EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]
