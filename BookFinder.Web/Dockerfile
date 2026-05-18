FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish BookFinder.Web/BookFinder.Web.csproj \
    -c Release -o /app/publish

FROM nginx:alpine AS final
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

# Copy Railway template into conf.d — bare server{} blocks are valid here
# nginx wraps conf.d files inside its own http{} block automatically
COPY BookFinder.Web/nginx.conf /etc/nginx/conf.d/default.conf.template

# Railway: envsubst writes to default.conf then starts nginx
# Docker Compose: volume mount already replaced default.conf, just start nginx
CMD ["/bin/sh", "-c", "if [ -n \"$PORT\" ]; then envsubst '$PORT' < /etc/nginx/conf.d/default.conf.template > /etc/nginx/conf.d/default.conf && nginx -g 'daemon off;'; else nginx -g 'daemon off;'; fi"]

EXPOSE 80