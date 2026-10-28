# Logistics App

A sample logistics application.

## Scope

The initial scope is a CRUD (Create, Read, Update, Delete) application for managing core logistics entities:
- **Vehicles**: Manage fleet vehicles and their details.
- **Drivers**: Manage driver records and assignments.
- **Routes**: Define and manage delivery/transit routes.

## Architecture

At its simplest, the application consists of:
- **Web UI**: A web page that allows users to view and interact with logistics data.
- **Web API**: A backend service that serves the web page and exposes RESTful APIs for performing CRUD operations on vehicles, drivers, and routes.

## API Specification & Testing

- **OpenAPI Specification**: [`docs/openapi/openapi.yaml`](docs/openapi/openapi.yaml) defines the v1 REST API contract for `/v1/vehicles`, `/v1/drivers`, and `/v1/routes` (including `/v1/routes/{routeId}/waypoints`), designed for Swagger tooling.

