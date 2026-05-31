FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/FacturXValidator/FacturXValidator.csproj src/FacturXValidator/
RUN dotnet restore src/FacturXValidator/FacturXValidator.csproj

COPY . .
RUN dotnet publish src/FacturXValidator/FacturXValidator.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8182

RUN mkdir -p /app/data/uploads /app/data/schemas
COPY --from=build /app/publish .

VOLUME ["/app/data"]
ENTRYPOINT ["dotnet", "FacturXValidator.dll"]
