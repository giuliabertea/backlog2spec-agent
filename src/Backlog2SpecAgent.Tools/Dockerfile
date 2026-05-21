# Build from repo root: docker build -f src/Backlog2SpecAgent.Tools/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /repo
COPY src/Backlog2SpecAgent.Tools/ src/Backlog2SpecAgent.Tools/
RUN dotnet publish src/Backlog2SpecAgent.Tools/Backlog2SpecAgent.Tools.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Backlog2SpecAgent.Tools.dll"]
