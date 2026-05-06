FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/Web/Web.csproj       Web/
COPY src/Shared/Shared.csproj Shared/
RUN dotnet restore Web/Web.csproj

COPY src/Web/    Web/
COPY src/Shared/ Shared/
RUN dotnet publish Web/Web.csproj -c Release -o /app/publish --no-restore

FROM nginx:alpine AS final
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY src/Web/nginx.conf          /etc/nginx/conf.d/default.conf
COPY src/Web/docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh
ENTRYPOINT ["/docker-entrypoint.sh"]
CMD ["nginx", "-g", "daemon off;"]
