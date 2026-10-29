using Asp.Versioning;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Logistics.Backend.Extensions;

public static class OpenApiExtensions
{
    public static IApiVersioningBuilder AddLogisticsOpenApi(this IApiVersioningBuilder apiVersioning)
    {
        apiVersioning.AddOpenApi(options =>
        {
            options.Document.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Logistics Management REST API",
                    Version = "1.0.0",
                    Description = """
                        RESTful HTTP API for logistics analysts to register and manage core fleet and transit resources:
                        - **Vehicles**: Fleet vehicles, payload capacities, and operational statuses.
                        - **Drivers**: Driver personnel records, commercial license classifications, and availability statuses.
                        - **Routes**: Transit and delivery routes composed of an ordered sequence of geographic waypoints.

                        ### REST Design Principles
                        This API follows:
                        - **Core REST Principles**: Resource-oriented plural nouns, standard HTTP method semantics (`GET`, `POST`, `PUT`, `PATCH`, `DELETE`), statelessness, and hypermedia/location headers.
                        - **Microsoft Azure REST API Guidelines**: Explicit URI path versioning (`/v1`), optimistic concurrency control via `ETag` and `If-Match` headers, `Idempotency-Key` support for safe retries on `POST`, and standardized `application/problem+json` error payloads.
                        - **Google Cloud API Design Guide (AIPs)**: Token-based pagination (`pageSize`, `pageToken`, `nextPageToken`, `totalCount`), standard collection filtering (`status`, `search`), and consistent resource lifecycle timestamps (`createdAt`, `updatedAt`).
                        """,
                    Contact = new OpenApiContact
                    {
                        Name = "Logistics Platform Team"
                    },
                    License = new OpenApiLicense
                    {
                        Name = "MIT",
                        Url = new Uri("https://opensource.org/licenses/MIT")
                    }
                };

                document.Servers = new List<OpenApiServer>
                {
                    new()
                    {
                        Url = "http://localhost:5045",
                        Description = "Local Development Server"
                    }
                };

                document.Tags = new HashSet<OpenApiTag>
                {
                    new()
                    {
                        Name = "Vehicles",
                        Description = "Operations for registering and managing fleet vehicles."
                    },
                    new()
                    {
                        Name = "Drivers",
                        Description = "Operations for registering and managing drivers and commercial license details."
                    },
                    new()
                    {
                        Name = "Routes",
                        Description = "Operations for defining and managing transit/delivery routes and their ordered waypoints."
                    }
                };

                if (document.Paths is not null)
                {
                    foreach ((string pathKey, IOpenApiPathItem pathItem) in document.Paths)
                    {
                        if (pathItem.Operations is null)
                        {
                            continue;
                        }

                        string entityName = pathKey.Contains("/vehicles") ? "vehicle" :
                                            pathKey.Contains("/drivers") ? "driver" :
                                            pathKey.Contains("/routes") ? "route" : "resource";
                        string entityNamePlural = pathKey.Contains("/vehicles") ? "vehicles" :
                                                  pathKey.Contains("/drivers") ? "drivers" :
                                                  pathKey.Contains("/routes") ? "routes" : "resources";
                        string entityCapitalized = char.ToUpper(entityName[0]) + entityName[1..];
                        string entityPluralCapitalized = char.ToUpper(entityNamePlural[0]) + entityNamePlural[1..];

                        foreach ((HttpMethod method, OpenApiOperation operation) in pathItem.Operations)
                        {
                            if (operation.Parameters is not null)
                            {
                                foreach (IOpenApiParameter parameter in operation.Parameters)
                                {
                                    if (parameter is OpenApiParameter p)
                                    {
                                        p.Description = p.Name switch
                                        {
                                            "pageSize" => "Maximum number of items to return per page (default 25, max 100).",
                                            "pageToken" => "Opaque continuation token returned from a previous list call (`nextPageToken`) to fetch the next page of results.",
                                            "orderBy" => "Sort expression (e.g., `createdAt desc`, `name asc`).",
                                            "search" => "Case-insensitive free-text filter applied across key identifying fields.",
                                            "status" => "Filter by operational status.",
                                            "type" => "Filter by vehicle type classification.",
                                            "minCapacityKg" => "Filter vehicles with payload capacity greater than or equal to this value in kilograms.",
                                            "licenseClass" => "Filter drivers by commercial driver license class.",
                                            "vehicleId" => "Unique identifier (UUID) of the vehicle.",
                                            "driverId" => "Unique identifier (UUID) of the driver.",
                                            "routeId" => "Unique identifier (UUID) of the route.",
                                            "Idempotency-Key" => "Optional client-generated unique key (e.g., UUID) to guarantee idempotent resource creation across network retries.",
                                            "If-Match" => "Optional entity tag (`ETag`) value for optimistic concurrency control. If the resource has been modified since this `ETag` was issued, the server returns `412 Precondition Failed`.",
                                            _ => p.Description
                                        };
                                    }
                                }
                            }

                            if (operation.Responses is null)
                            {
                                continue;
                            }

                            if (operation.Responses.TryGetValue("201", out IOpenApiResponse? createdResponse) &&
                                createdResponse is OpenApiResponse concreteCreated)
                            {
                                concreteCreated.Description = $"{entityCapitalized} successfully registered.";
                                concreteCreated.Headers ??= new Dictionary<string, IOpenApiHeader>();
                                concreteCreated.Headers["Location"] = new OpenApiHeader
                                {
                                    Description = "Canonical URI of the newly created resource.",
                                    Schema = new OpenApiSchema
                                    {
                                        Type = JsonSchemaType.String,
                                        Format = "uri"
                                    }
                                };
                                concreteCreated.Headers["ETag"] = new OpenApiHeader
                                {
                                    Description = "Opaque entity tag representing the current version of the resource for optimistic concurrency control.",
                                    Schema = new OpenApiSchema
                                    {
                                        Type = JsonSchemaType.String
                                    }
                                };
                            }

                            bool isItemOrWaypointsPath = pathKey.Contains('{');
                            if (operation.Responses.TryGetValue("200", out IOpenApiResponse? okResponse) &&
                                okResponse is OpenApiResponse concreteOk)
                            {
                                if (pathKey.EndsWith("/waypoints"))
                                {
                                    concreteOk.Description = method == HttpMethod.Get
                                        ? "Ordered sequence of waypoints for the route."
                                        : "Ordered waypoints successfully replaced.";
                                }
                                else if (isItemOrWaypointsPath)
                                {
                                    concreteOk.Description = method == HttpMethod.Get
                                        ? $"{entityCapitalized} details."
                                        : method == HttpMethod.Put
                                            ? $"{entityCapitalized} successfully replaced."
                                            : $"{entityCapitalized} successfully updated.";
                                }
                                else
                                {
                                    concreteOk.Description = $"Paginated list of {entityNamePlural}.";
                                }

                                if (isItemOrWaypointsPath)
                                {
                                    concreteOk.Headers ??= new Dictionary<string, IOpenApiHeader>();
                                    concreteOk.Headers["ETag"] = new OpenApiHeader
                                    {
                                        Description = "Opaque entity tag representing the current version of the resource for optimistic concurrency control.",
                                        Schema = new OpenApiSchema
                                        {
                                            Type = JsonSchemaType.String
                                        }
                                    };
                                }
                            }

                            if (operation.Responses.TryGetValue("204", out IOpenApiResponse? noContentResponse) &&
                                noContentResponse is OpenApiResponse concreteNoContent)
                            {
                                concreteNoContent.Description = $"{entityCapitalized} successfully deleted.";
                            }

                            if (operation.Responses.TryGetValue("400", out IOpenApiResponse? badRequestResponse) &&
                                badRequestResponse is OpenApiResponse concreteBadRequest)
                            {
                                concreteBadRequest.Description = "Request validation failed (`400 Bad Request`).";
                            }

                            if (operation.Responses.TryGetValue("404", out IOpenApiResponse? notFoundResponse) &&
                                notFoundResponse is OpenApiResponse concreteNotFound)
                            {
                                concreteNotFound.Description = "The requested resource was not found (`404 Not Found`).";
                            }

                            if (operation.Responses.TryGetValue("409", out IOpenApiResponse? conflictResponse) &&
                                conflictResponse is OpenApiResponse concreteConflict)
                            {
                                concreteConflict.Description = "A conflict occurred with the current state of the target resource (`409 Conflict`), such as a duplicate unique identifier (VIN, license plate, employee number, or route code).";
                            }

                            if (operation.Responses.TryGetValue("412", out IOpenApiResponse? preconditionResponse) &&
                                preconditionResponse is OpenApiResponse concretePrecondition)
                            {
                                concretePrecondition.Description = "The `If-Match` precondition failed (`412 Precondition Failed`) because the resource was modified by another request.";
                            }

                            if (operation.Responses.TryGetValue("500", out IOpenApiResponse? serverErrorResponse) &&
                                serverErrorResponse is OpenApiResponse concreteServerError)
                            {
                                concreteServerError.Description = "An unexpected server error occurred (`500 Internal Server Error`).";
                            }
                        }
                    }
                }

                return Task.CompletedTask;
            });
        });

        return apiVersioning;
    }
}
