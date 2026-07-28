FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ZEstate.Core/*.csproj ZEstate.Core/
COPY ZEstate.Infrastructure/*.csproj ZEstate.Infrastructure/
COPY ZEstateApi/*.csproj ZEstateApi/
RUN dotnet restore ZEstateApi/ZEstateApi.csproj

COPY ZEstate.Core/ ZEstate.Core/
COPY ZEstate.Infrastructure/ ZEstate.Infrastructure/
COPY ZEstateApi/ ZEstateApi/
RUN dotnet publish ZEstateApi/ZEstateApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "ZEstateApi.dll"]
