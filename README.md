# TrackMyDelivery

TrackMyDelivery is a small delivery tracking platform built to showcase a clean backend structure with an event-driven workflow that is still easy to run locally.

The goal is to keep the repo practical:

- .Net 10 API for delivery commands and queries
- Domain model for delivery lifecycle rules
- SQLite for local persistence
- Outbox pattern for durable delivery event storage
- RabbitMQ for delivery event handoff between the API and worker
- Background worker that updates the tracking timeline from delivery messages

## Solution structure

- `TrackMyDelivery.Api`
  HTTP endpoints, Swagger, health checks, and application composition
- `TrackMyDelivery.Application`
  Commands, queries, contracts, and repository interfaces
- `TrackMyDelivery.Domain`
  Delivery aggregate, statuses, and domain events
- `TrackMyDelivery.Infrastructure`
  SQLite persistence, stored delivery events, RabbitMQ publishing, and tracking timeline updates
- `TrackMyDelivery.Worker`
  Background service that consumes delivery messages and updates the tracking timeline
- `TrackMyDelivery.Domain.Tests`
  Focused domain, persistence, and API integration tests

## Architecture diagram

```mermaid
flowchart LR
    Client["Client / Swagger"] --> Api["TrackMyDelivery.Api"]

    subgraph WriteFlow["Write flow"]
        Api --> App["Application handlers"]
        App --> Domain["Delivery aggregate"]
        Domain --> Events["Delivery events raised"]
        App --> DeliveryStore[("deliveries")]
        App --> StoredEvents[("outbox_messages")]
    end

    subgraph AsyncFlow["Async flow"]
        StoredEvents --> Publisher["Stored delivery event publisher"]
        Publisher --> Broker[("RabbitMQ")]
        Broker --> Worker["TrackMyDelivery.Worker"]
        Worker --> TimelineStore[("tracking_events")]
        Worker --> FailedEvents[("failed delivery queue")]
        Worker --> FailedLedger[("failed_delivery_messages")]
    end

    subgraph ReadFlow["Read flow"]
        Api --> QueryHandlers["Application queries"]
        QueryHandlers --> TimelineStore
        QueryHandlers --> DeliveryStore
    end

    DeliveryStore -. stored in .-> Sqlite[("SQLite database")]
    StoredEvents -. stored in .-> Sqlite
    TimelineStore -. stored in .-> Sqlite
    FailedLedger -. stored in .-> Sqlite
```

## Layered view

```mermaid
flowchart TB
    Api["TrackMyDelivery.Api
    Controllers
    Swagger
    Health checks"]

    Application["TrackMyDelivery.Application
    Commands
    Queries
    Contracts
    Interfaces"]

    Domain["TrackMyDelivery.Domain
    Delivery aggregate
    Statuses
    Domain events"]

    Infrastructure["TrackMyDelivery.Infrastructure
    SQLite repositories
    Stored delivery events
    RabbitMQ publishing
    Tracking timeline updates"]

    Worker["TrackMyDelivery.Worker
    RabbitMQ consumer
    Async processing"]

    Api --> Application
    Application --> Domain
    Application --> Infrastructure
    Worker --> Infrastructure
    Infrastructure --> Domain
```

## How it works

1. A client creates a delivery through the API.
2. The domain raises a delivery event.
3. The API persists the delivery and stores the delivery event in the outbox.
4. A background publisher reads stored delivery events and publishes them to RabbitMQ.
5. The worker consumes delivery messages from RabbitMQ.
6. The worker writes tracking events into the tracking timeline table.
7. Failed delivery messages are retried a limited number of times and then moved to a failed-delivery queue.
8. Parked delivery failures are also written to the database so they can be reviewed and replayed manually.
9. The API returns the tracking timeline from that projection.

This keeps the write flow durable, the boundary crossing explicit, and the read model separate enough to demonstrate the pattern without making the repo hard to follow.

Correlation IDs are carried from the API boundary into stored delivery events, published delivery messages, and worker logs so one request can be traced through the async flow.

RabbitMQ messaging is disabled by default in local settings, so the repo can still be explored without a broker running.

## Local run

Requirements:

- .NET 10 SDK
- RabbitMQ only if you want to run the broker-backed async flow locally

Run the full local runtime with Docker:

```powershell
docker compose up --build
```

This starts:

- API: `http://localhost:5111`
- RabbitMQ broker: `localhost:5672`
- RabbitMQ management UI: `http://localhost:15672` (`guest` / `guest`)
- Worker: consumes delivery events and updates the tracking timeline
- Shared SQLite volume: `/data/track-my-delivery.db` inside the API and worker containers

The Compose runtime enables RabbitMQ messaging for both the API and worker through environment variables, while keeping the default appsettings broker-free for simple local exploration.

Run the API:

```powershell
dotnet run --project .\TrackMyDelivery.Api\TrackMyDelivery.Api.csproj --launch-profile https
```

Run the worker in another terminal:

```powershell
dotnet run --project .\TrackMyDelivery.Worker\TrackMyDelivery.Worker.csproj
```

Enable RabbitMQ publishing and consumption by setting `Messaging:Enabled` to `true` in:

- `TrackMyDelivery.Api\appsettings.json`
- `TrackMyDelivery.Worker\appsettings.json`

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
5. `POST /api/operations/replay-failed-delivery-messages?maxCount=10`

The tracking endpoint only becomes interesting once the worker is running, because the worker is what turns stored delivery events into tracking timeline entries.

The replay endpoint is there for failure recovery. It republishes up to the requested number of parked delivery messages and marks them as replayed in the failure ledger.

You can run the same flow from:

- `docs\demo-flow.http`

Set `@deliveryId` in that file after creating a delivery, then continue through the courier assignment, status update, and tracking timeline requests.

## Run tests

```powershell
dotnet test .\TrackMyDelivery.slnx
```

The test suite covers:

- delivery lifecycle rules in the domain model
- outbox persistence, publish state, and retry behavior
- parked delivery replay behavior
- delivery message attempt tracking
- correlation propagation
- API health check and delivery flow integration

## Logs

The API and worker write structured logs to the console and rolling files:

- API logs: `TrackMyDelivery.Api\logs\log-*.txt`
- Worker logs: `TrackMyDelivery.Worker\logs\worker-log-*.txt`

Useful things to look for:

- delivery IDs and tracking numbers during command handling
- stored delivery event publish counts
- worker messages showing delivery message consumption and tracking timeline updates
- retry and failed-delivery queue messages when delivery message handling fails
- replay messages when parked delivery failures are sent back through RabbitMQ

## Why SQLite

SQLite keeps the repo runnable with almost no setup:

- no cloud account
- no secrets
- no local database server
- one file on disk

That keeps the focus on architecture and flow instead of environment setup.

## Future improvements

- Add deployment notes for Azure hosting
- Add auth around operational endpoints before any shared deployment
