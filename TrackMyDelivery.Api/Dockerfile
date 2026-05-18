FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TrackMyDelivery.slnx ./
COPY TrackMyDelivery.Api/TrackMyDelivery.Api.csproj TrackMyDelivery.Api/
COPY TrackMyDelivery.Application/TrackMydelivery.Application.csproj TrackMyDelivery.Application/
COPY TrackMyDelivery.Domain/TrackMyDelivery.Domain.csproj TrackMyDelivery.Domain/
COPY TrackMyDelivery.Infrastructure/TrackMyDelivery.Infrastructure.csproj TrackMyDelivery.Infrastructure/
RUN dotnet restore TrackMyDelivery.Api/TrackMyDelivery.Api.csproj

COPY . .
RUN dotnet publish TrackMyDelivery.Api/TrackMyDelivery.Api.csproj --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "TrackMyDelivery.Api.dll"]
