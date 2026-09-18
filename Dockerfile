FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["SantanderHackerNews.Api/SantanderHackerNews.Api.csproj", "SantanderHackerNews.Api/"]
RUN dotnet restore "SantanderHackerNews.Api/SantanderHackerNews.Api.csproj"
COPY . .
RUN dotnet publish "SantanderHackerNews.Api/SantanderHackerNews.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "SantanderHackerNews.Api.dll"]
