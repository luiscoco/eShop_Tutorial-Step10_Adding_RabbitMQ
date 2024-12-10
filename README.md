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

**Microsoft.Extensions.DependencyInjection.Abstractions**:



**Microsoft.Extensions.Options**:



### 1.2. Abstractions

**IEventBus**: Handles event publishing

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



**IEventBusBuilder.cs**

```csharp
namespace Microsoft.Extensions.DependencyInjection;

public interface IEventBusBuilder
{
    public IServiceCollection Services { get; }
}
```

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



### 1.4. Extensions






## 2. EventBusRabbitMQ

**Purpose**: Implements the EventBus abstraction using RabbitMQ as the messaging infrastructure

**Key Responsibilities**:

Integrate with RabbitMQ to publish and subscribe to messages

Provide extensions and configurations specific to RabbitMQ

**Notable Components**:

RabbitMQEventBus.cs: The primary implementation for RabbitMQ-based event handling

RabbitMqDependencyInjectionExtensions.cs: Facilitates dependency injection setup for RabbitMQ in the application

RabbitMQTelemetry.cs: Possibly handles monitoring or logging specific to RabbitMQ

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





