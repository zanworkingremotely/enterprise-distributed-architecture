# TrackMyDelivery

TrackMyDelivery is a small delivery tracking platform built to showcase a clean backend structure with an event-driven workflow that is still easy to run locally.

The goal is to keep the repo practical:

- ASP.NET Core API for delivery commands and queries
- Domain model for delivery lifecycle rules
- SQLite for local persistence
- Outbox pattern for durable event storage
- Background worker that processes outbox messages into a tracking timeline

## Solution structure

- `TrackMyDelivery.Api`
  HTTP endpoints, Swagger, health checks, and application composition
- `TrackMyDelivery.Application`
  Commands, queries, contracts, and repository interfaces
- `TrackMyDelivery.Domain`
  Delivery aggregate, statuses, and domain events
- `TrackMyDelivery.Infrastructure`
  SQLite persistence, durable event storage, and tracking timeline updates
- `TrackMyDelivery.Worker`
  Background service that updates the delivery tracking timeline
- `TrackMyDelivery.Domain.Tests`
  Focused domain tests for delivery lifecycle behavior

## How it works

1. A client creates a delivery through the API.
2. The domain raises a delivery event.
3. The API persists the delivery and stores the delivery event for background processing.
4. The worker polls stored delivery events.
5. The worker writes tracking events into the tracking timeline table.
6. The API returns the tracking timeline from that projection.

This keeps the write flow and the read model separate enough to demonstrate the pattern without making the repo hard to run.

## Local run

Requirements:

- .NET 10 SDK

Run the API:

```powershell
dotnet run --project .\TrackMyDelivery.Api\TrackMyDelivery.Api.csproj --launch-profile https
```

Run the worker in another terminal:

```powershell
dotnet run --project .\TrackMyDelivery.Worker\TrackMyDelivery.Worker.csproj
```

Useful URLs:

- Swagger: `https://localhost:7226/swagger`
- Health check: `https://localhost:7226/health`

SQLite database file:

- `TrackMyDelivery.SharedData\track-my-delivery.db`

## Example flow

1. `POST /api/deliveries`
2. `POST /api/deliveries/{deliveryId}/assign-courier`
3. `POST /api/deliveries/{deliveryId}/status`
4. `GET /api/deliveries/{deliveryId}/tracking`

The tracking endpoint only becomes interesting once the worker is running, because the worker is what turns stored delivery events into tracking timeline entries.

## Run tests

```powershell
dotnet test .\TrackMyDelivery.slnx
```

## Why SQLite

SQLite keeps the repo runnable with almost no setup:

- no cloud account
- no secrets
- no local database server
- one file on disk

That keeps the focus on architecture and flow instead of environment setup.

## Future improvements

- Replace the polling worker with a broker-backed implementation
- Add idempotency and retry policy around event processing
- Add API integration tests
- Add deployment notes for Azure hosting
