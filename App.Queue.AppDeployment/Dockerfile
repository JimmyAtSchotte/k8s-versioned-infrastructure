FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore App.Queue.AppDeployment
RUN dotnet build App.Queue.AppDeployment -c Release -o /app/build

FROM build AS publish
RUN dotnet publish App.Queue.AppDeployment -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "App.Queue.AppDeployment.dll"]