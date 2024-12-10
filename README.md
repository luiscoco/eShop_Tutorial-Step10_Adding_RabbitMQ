# Building 'eShop' from Zero to Hero: Adding RabbitMQ

In the **eShop** application, **RabbitMQ** serves as the message broker responsible for handling events and enabling seamless **communication between the back-end APIs**

This inter-API communication system is implemented using three main classes: **EventBus**, **EventBusRabbitMQ**, and **IntegrationEventLogEF**

These projects collectively handle messaging and event-driven architecture:

**EventBus** provides the abstraction

**EventBusRabbitMQ** integrates with RabbitMQ for actual messaging

**IntegrationEventLogEF** ensures reliability by persisting integration events, enabling retry mechanisms in case of failure

We review the eShop architecture general picture and how **RabbitMQ** is the message broker for coordinating actions between APIs

![image](https://github.com/user-attachments/assets/8d6f98ac-74a5-4b87-a807-229642404671)

## 1. EventBus

**Purpose**: Defines an abstraction layer for a publish-subscribe messaging system

**Key Responsibilities**:

Provide interfaces and base implementations for event publishing and subscription

Handle generic event handling mechanisms

**Notable Components**:

Abstractions: Contains interfaces and contracts for event bus implementations

Events: Defines event-related base classes or types

Extensions: Utility or extension methods to support the event bus

### 1.1. Load Nuget Packages

**Microsoft.Extensions.DependencyInjection.Abstractions**: Provides the core abstractions for dependency injection and service management in .NET

**Microsoft.Extensions.Options**: Provides a structured way to manage and validate application configuration using the Options pattern

### 1.2. Abstractions

**IEventBus** handles event publishing

**IEventBus.cs** Defines the contract for an event bus

**Purpose**: It specifies a method **PublishAsync** that allows asynchronous publishing of integration events (IntegrationEvent)

**Usage**: Any class implementing **IEventBus** is responsible for defining how events are published, e.g., sending messages to a message queue (like RabbitMQ or Azure Service Bus)

**IEventBus.cs**

```csharp
namespace eShop.EventBus.Abstractions;

public interface IEventBus
{
    Task PublishAsync(IntegrationEvent @event);
}
```

**IEventBusBuilder** configures the event bus and its dependencies

**IEventBusBuilder.cs** defines a builder pattern for configuring an event bus

**Purpose**: Provides access to the **IServiceCollection** so that services related to the event bus can be registered with the dependency injection (DI) container

**Usage**: Used during application startup to configure event bus services

**IEventBusBuilder.cs**

```csharp
namespace Microsoft.Extensions.DependencyInjection;

public interface IEventBusBuilder
{
    public IServiceCollection Services { get; }
}
```

**IIntegrationEventHandler** defines how to handle incoming events

**IIntegrationEventHandler.cs** Defines the contract for handling integration events

Generic Interface ```IIntegrationEventHandler<TIntegrationEvent>```: Handles events of a specific type (TIntegrationEvent), which must derive from IntegrationEvent

Provides the Handle method that processes the event. Includes a default implementation for the non-generic Handle method that casts and delegates to the generic one

Non-Generic Interface ```IIntegrationEventHandler```: Defines a general-purpose Handle method for all IntegrationEvent types

Usage: Event handlers implement these interfaces to define custom logic for processing specific integration events

**IIntegrationEventHandler.cs**

```csharp
namespace eShop.EventBus.Abstractions;

public interface IIntegrationEventHandler<in TIntegrationEvent> : IIntegrationEventHandler
    where TIntegrationEvent : IntegrationEvent
{
    Task Handle(TIntegrationEvent @event);

    Task IIntegrationEventHandler.Handle(IntegrationEvent @event) => Handle((TIntegrationEvent)@event);
}

public interface IIntegrationEventHandler
{
    Task Handle(IntegrationEvent @event);
}
```

**EventBusSubscriptionInfo** manages event type metadata and serialization settings

**EventBusSubscriptionInfo.cs** Manages subscription information for the event bus

**EventTypes**: A dictionary that maps event names (as strings) to their corresponding CLR types. This is used to look up the type of an event when processing messages

**JsonSerializerOptions**: Specifies how events should be serialized/deserialized using System.Text.Json

**DefaultSerializerOptions**: Defines default serialization options, including a type resolver (IJsonTypeInfoResolver) for efficient type handling

**CreateDefaultTypeResolver**: Creates a default type resolver for handling serialization metadata

**Usage**: This class is used internally by the event bus to manage event subscriptions and ensure proper serialization/deserialization of events

**EventBusSubscriptionInfo.cs**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace eShop.EventBus.Abstractions;

public class EventBusSubscriptionInfo
{
    public Dictionary<string, Type> EventTypes { get; } = [];

    public JsonSerializerOptions JsonSerializerOptions { get; } = new(DefaultSerializerOptions);

    internal static readonly JsonSerializerOptions DefaultSerializerOptions = new()
    {
        TypeInfoResolver = JsonSerializer.IsReflectionEnabledByDefault ? CreateDefaultTypeResolver() : JsonTypeInfoResolver.Combine()
    };

#pragma warning disable IL2026
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
    private static IJsonTypeInfoResolver CreateDefaultTypeResolver()
        => new DefaultJsonTypeInfoResolver();
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IL2026
}
```

### 1.3. Events

This **IntegrationEvent** class is a base model for integration events in a distributed system

It ensures that all events have a unique identifier (**Id**) for tracking purposes, and record the exact time they were created (**CreationDate**)

This is often used in **event-driven** architectures, where events are transmitted across different services (e.g., in microservices)

**IntegrationEvent.cs**

```csharp
namespace eShop.EventBus.Events;

public record IntegrationEvent
{
    public IntegrationEvent()
    {
        Id = Guid.NewGuid();
        CreationDate = DateTime.UtcNow;
    }

    [JsonInclude]
    public Guid Id { get; set; }

    [JsonInclude]
    public DateTime CreationDate { get; set; }
}
```

### 1.4. Extensions

This code defines a set of **extension methods** for configuring and extending the functionality of an event bus in a .NET application

This code is part of a framework for setting up and managing an event-driven system using a message bus. It focuses on:

Customizing **JSON serialization options** for events: **ConfigureJsonOptions**

Defining **subscriptions to events** and their handlers with DI: **AddSubscription**

Supporting multiple handlers for the same event type

Ensuring event type mappings are tracked for runtime efficiency and correctness

**EventBusBuilderExtensions.cs**

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using eShop.EventBus.Abstractions;
using eShop.EventBus.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class EventBusBuilderExtensions
{
    public static IEventBusBuilder ConfigureJsonOptions(this IEventBusBuilder eventBusBuilder, Action<JsonSerializerOptions> configure)
    {
        eventBusBuilder.Services.Configure<EventBusSubscriptionInfo>(o =>
        {
            configure(o.JsonSerializerOptions);
        });

        return eventBusBuilder;
    }

    public static IEventBusBuilder AddSubscription<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TH>(this IEventBusBuilder eventBusBuilder)
        where T : IntegrationEvent
        where TH : class, IIntegrationEventHandler<T>
    {
        // Use keyed services to register multiple handlers for the same event type
        // the consumer can use IKeyedServiceProvider.GetKeyedService<IIntegrationEventHandler>(typeof(T)) to get all
        // handlers for the event type.
        eventBusBuilder.Services.AddKeyedTransient<IIntegrationEventHandler, TH>(typeof(T));

        eventBusBuilder.Services.Configure<EventBusSubscriptionInfo>(o =>
        {
            // Keep track of all registered event types and their name mapping. We send these event types over the message bus
            // and we don't want to do Type.GetType, so we keep track of the name mapping here.

            // This list will also be used to subscribe to events from the underlying message broker implementation.
            o.EventTypes[typeof(T).Name] = typeof(T);
        });

        return eventBusBuilder;
    }
}
```

**GenericTypeExtensions.cs**

```csharp
namespace eShop.EventBus.Extensions;

public static class GenericTypeExtensions
{
    public static string GetGenericTypeName(this Type type)
    {
        string typeName;

        if (type.IsGenericType)
        {
            var genericTypes = string.Join(",", type.GetGenericArguments().Select(t => t.Name).ToArray());
            typeName = $"{type.Name.Remove(type.Name.IndexOf('`'))}<{genericTypes}>";
        }
        else
        {
            typeName = type.Name;
        }

        return typeName;
    }

    public static string GetGenericTypeName(this object @object)
    {
        return @object.GetType().GetGenericTypeName();
    }
}
```

## 2. EventBusRabbitMQ

**Purpose**: Implements the EventBus abstraction using RabbitMQ as the messaging infrastructure

**Key Responsibilities**:

Integrate with RabbitMQ to publish and subscribe to messages

Provide extensions and configurations specific to RabbitMQ

**Notable Components**:

RabbitMQEventBus.cs: The primary implementation for RabbitMQ-based event handling

RabbitMqDependencyInjectionExtensions.cs: Facilitates dependency injection setup for RabbitMQ in the application

RabbitMQTelemetry.cs: Possibly handles monitoring or logging specific to RabbitMQ

### 2.1. Load Nuge Packages

**Aspire.RabbitMQ.Client**: Adds support for messaging with RabbitMQ

Purpose: A library for interacting with RabbitMQ, a popular message broker used for implementing messaging and queuing in distributed systems

Features: Provides client-side tools for sending and receiving messages through RabbitMQ

Likely wraps or extends the base RabbitMQ client (RabbitMQ.Client) to add additional functionality or simplify usage

Typical Use Case: Used in applications that need to implement message-based communication between services, such as event-driven architectures or microservices

**Microsoft.Extensions.Options.ConfigurationExtensions**: Provides configuration binding and management for the application's settings

Purpose: Extends the functionality of the Options Pattern in .NET by providing integration with configuration sources, such as appsettings.json or environment variables

Features: Enables seamless binding of configuration data from IConfiguration sources into strongly-typed option objects

Simplifies accessing settings in a structured and type-safe manner

Typical Use Case: Automatically mapping configuration sections from JSON or environment variables into strongly-typed classes

Ensuring that application settings are easy to manage and validate

**Polly.Core**: Adds reliability features for handling transient faults and improving application resilience

Purpose: Polly is a resilience and transient fault-handling library for .NET applications. The Polly.Core package represents the core functionality of the library

Features: Implements resilience strategies like retries, circuit breakers, timeouts, bulkhead isolation, and fallback mechanisms 

Lightweight and extensible, designed for integration into applications needing fault tolerance and reliability

Typical Use Case: handling intermittent failures in external systems like APIs, databases, or message brokers

Ensuring the application is resilient against network issues or service outages

![image](https://github.com/user-attachments/assets/b02134b1-ac49-4ee1-8348-a5cf75a1fe53)

### 2.2.





### 2.3.





### 2.4.





## 3. IntegrationEventLogEF

Purpose: Manages the persistence of integration events in a database for reliable delivery.

**Key Responsibilities**:

Log and track the state of integration events (e.g., pending, published, failed)

Use Entity Framework (EF) to persist events for durability

**Notable Components**:

IntegrationEventLogEntry.cs: Represents the structure of an event log entry in the database

EventStateEnum.cs: Enumerates the states of an integration event (e.g., Pending, Published)

Services: Likely includes logic to manage and query integration event logs

IntegrationLogExtensions.cs: Provides utility methods for working with event logs


## 4. 





