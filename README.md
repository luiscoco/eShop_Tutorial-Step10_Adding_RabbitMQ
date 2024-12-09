# Building 'eShop' from Zero to Hero: Adding RabbitMQ

In the **eShop** application, **RabbitMQ** serves as the message broker responsible for handling events and enabling seamless **communication between the back-end APIs**

This inter-API communication system is implemented using three main classes: **EventBus**, **EventBusRabbitMQ**, and **IntegrationEventLogEF**

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



## 3. IntegrationEventLogEF




## 4. 





