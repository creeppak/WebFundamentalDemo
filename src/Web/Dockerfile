FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Web/Web.csproj       src/Web/
COPY src/Shared/Shared.csproj src/Shared/
RUN dotnet restore src/Web/Web.csproj

COPY src/ src/
RUN dotnet publish src/Web/Web.csproj -c Release -o /app/publish --no-restore

FROM nginx:alpine AS final
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY src/Web/nginx.conf          /etc/nginx/conf.d/default.conf
COPY src/Web/docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh
ENTRYPOINT ["/docker-entrypoint.sh"]
CMD ["nginx", "-g", "daemon off;"]