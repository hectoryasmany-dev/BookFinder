FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BookFinder.Application/BookFinder.Application.csproj BookFinder.Application/
COPY BookFinder.Api/BookFinder.Api.csproj BookFinder.Api/
RUN dotnet restore BookFinder.Api/BookFinder.Api.csproj

COPY BookFinder.Application/ BookFinder.Application/
COPY BookFinder.Api/ BookFinder.Api/
RUN dotnet publish BookFinder.Api/BookFinder.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "BookFinder.Api.dll"]