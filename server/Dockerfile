FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Restaurant.Api/Restaurant.Api.csproj Restaurant.Api/
RUN dotnet restore Restaurant.Api/Restaurant.Api.csproj
COPY Restaurant.Api/ Restaurant.Api/
RUN dotnet publish Restaurant.Api/Restaurant.Api.csproj -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Restaurant.Api.dll"]
