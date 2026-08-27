FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/PqrsSaas.Api/PqrsSaas.Api.csproj
RUN dotnet publish src/PqrsSaas.Api/PqrsSaas.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "PqrsSaas.Api.dll"]
