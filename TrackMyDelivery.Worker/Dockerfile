FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TrackMyDelivery.slnx ./
COPY TrackMyDelivery.Worker/TrackMyDelivery.Worker.csproj TrackMyDelivery.Worker/
COPY TrackMyDelivery.Application/TrackMydelivery.Application.csproj TrackMyDelivery.Application/
COPY TrackMyDelivery.Domain/TrackMyDelivery.Domain.csproj TrackMyDelivery.Domain/
COPY TrackMyDelivery.Infrastructure/TrackMyDelivery.Infrastructure.csproj TrackMyDelivery.Infrastructure/
RUN dotnet restore TrackMyDelivery.Worker/TrackMyDelivery.Worker.csproj

COPY . .
RUN dotnet publish TrackMyDelivery.Worker/TrackMyDelivery.Worker.csproj --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TrackMyDelivery.Worker.dll"]
