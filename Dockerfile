FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY TeamTracker.csproj .
RUN dotnet restore TeamTracker.csproj
COPY . .
RUN dotnet publish TeamTracker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /app/App_Data && chown -R $APP_UID /app/App_Data
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Demo__DailyReset=true

EXPOSE 8080
ENTRYPOINT ["dotnet", "TeamTracker.dll"]
