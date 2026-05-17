FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BookFinder.Web/BookFinder.Web.csproj BookFinder.Web/
RUN dotnet restore BookFinder.Web/BookFinder.Web.csproj

COPY BookFinder.Web/ BookFinder.Web/
RUN dotnet publish BookFinder.Web/BookFinder.Web.csproj -c Release -o /app/publish --no-restore

FROM nginx:alpine AS final
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY BookFinder.Web/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
