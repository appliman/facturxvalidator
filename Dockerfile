FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY FacturXValidatorSaas/FacturXValidatorSaas.csproj FacturXValidatorSaas/
RUN dotnet restore FacturXValidatorSaas/FacturXValidatorSaas.csproj

COPY . .
RUN dotnet publish FacturXValidatorSaas/FacturXValidatorSaas.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

RUN mkdir -p /app/data/uploads /app/data/schemas
COPY --from=build /app/publish .

VOLUME ["/app/data"]
ENTRYPOINT ["dotnet", "FacturXValidatorSaas.dll"]
