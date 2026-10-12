FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY CampusEats.Api/CampusEats.Api.csproj CampusEats.Api/
RUN dotnet restore CampusEats.Api/CampusEats.Api.csproj
COPY CampusEats.Api/ CampusEats.Api/
RUN dotnet publish CampusEats.Api/CampusEats.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "CampusEats.Api.dll"]
