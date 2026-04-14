# TrackMyDelivery Walkthrough

## What this repo is trying to prove

This repo is meant to show practical backend architecture rather than just CRUD endpoints.

The main ideas it demonstrates are:

- clear separation between API, application, domain, infrastructure, and worker responsibilities
- asynchronous event processing without requiring a full distributed system to run locally
- a write flow and a read flow that are related but not tightly coupled
- a design that is easy to explain and easy to run

## Why I chose this shape

I deliberately kept this as a modular monolith.

That means:

- the code is split into architectural layers
- the responsibilities are separated clearly
- the async workflow is real
- local setup is still simple

I did not want to force microservices just for effect, because that often creates noise without improving the actual design story.

## Project-by-project explanation

### TrackMyDelivery.Api

This is the HTTP entry point.

Its job is to:

- receive requests
- bind request models
- call the relevant application handlers
- expose Swagger and health checks

It should not contain business rules or persistence logic.

### TrackMyDelivery.Application

This is the use-case layer.

Its job is to:

- define commands and queries
- coordinate use cases
- depend on interfaces rather than concrete persistence

This is the closest equivalent to a traditional service layer, but a bit more structured.

### TrackMyDelivery.Domain

This is where the business behavior lives.

The `Delivery` aggregate controls:

- creating a delivery
- assigning a courier
- updating delivery status
- raising domain events when state changes

The goal is that business rules belong here rather than leaking into controllers or repository code.

### TrackMyDelivery.Infrastructure

This is where technical implementation details live.

In this repo it handles:

- SQLite persistence
- storing delivery events for later processing
- reading tracking timeline data
- updating the tracking timeline from stored delivery events

The application layer depends on interfaces. Infrastructure provides the actual implementations.

### TrackMyDelivery.Worker

This is a background process that updates the tracking timeline.

Its job is to:

- poll stored delivery events
- convert them into tracking timeline entries
- keep the query side up to date

This is the async part of the system.

## End-to-end request flow

### Create delivery

1. API receives `POST /api/deliveries`
2. Application handler creates a `Delivery`
3. Domain raises `DeliveryCreatedDomainEvent`
4. Delivery is saved
5. Delivery event is stored for background processing
6. Worker later reads that event and writes a tracking entry

### Assign courier

1. API receives `POST /api/deliveries/{id}/assign-courier`
2. Application loads the delivery
3. Domain updates state and raises `CourierAssignedDomainEvent`
4. Delivery is updated in SQLite
5. Delivery event is stored for background processing
6. Worker updates the tracking timeline

### Update status

1. API receives `POST /api/deliveries/{id}/status`
2. Application loads the delivery
3. Domain validates the transition and updates state
4. Domain raises `DeliveryStatusUpdatedDomainEvent`
5. Delivery is updated in SQLite
6. Worker writes a tracking timeline entry

## Why the worker exists

The worker is there to show asynchronous processing in a practical way.

Instead of updating every read model inline during the request, the system stores delivery events and lets the worker build the tracking timeline separately.

That gives a cleaner architecture story:

- request handling stays focused on the write operation
- tracking history is built asynchronously
- the design is closer to how larger systems are often shaped

## Why SQLite

SQLite was chosen for practicality.

It gives:

- zero server setup
- one database file
- easy local cloning and execution
- enough realism to demonstrate persistence and async processing

This keeps the barrier to running the repo low, which matters for a portfolio project.

## Why not jump straight to Azure services

I have Azure experience, but for this repo I wanted something runnable immediately.

If I had used cloud services too early, the repo would require:

- secrets
- cloud resource setup
- more environment documentation
- more friction for reviewers

So the current version optimizes for clarity and local execution first.

## What I would say are the strongest architecture choices here

- modular monolith instead of fake microservices
- domain events to reflect business state changes
- durable storage of delivery events for async processing
- a separate worker to update the tracking timeline
- SQLite to keep the repo runnable

## What I would improve next

- add API or integration tests
- add an architecture diagram
- tighten naming further where needed
- evolve the stored event processing toward a more production-grade eventing story
- add deployment notes for cloud hosting

## Short interview summary

This repo is a delivery tracking backend that demonstrates a practical event-driven architecture inside a modular monolith. The API handles commands and queries, the domain owns business rules and raises delivery events, the infrastructure persists both delivery state and delivery events in SQLite, and a worker processes those events into a tracking timeline. I chose SQLite and a local worker to keep the repo easy to run while still showing patterns I would use in a larger distributed design.
