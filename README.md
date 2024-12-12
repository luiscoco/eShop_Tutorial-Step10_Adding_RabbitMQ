# Building 'eShop' from Zero to Hero: Adding RabbitMQ

In the **eShop** application, **RabbitMQ** serves as the **message broker** responsible for handling events and enabling seamless **communication between the back-end APIs**

![image](https://github.com/user-attachments/assets/47639548-107d-42bf-b511-7ee5a49f446d)

This inter-API communication system is implemented using three main projects: **EventBus**, **EventBusRabbitMQ**, and **IntegrationEventLogEF**

These projects collectively handle messaging and event-driven architecture:

**EventBus** provides the abstraction

**EventBusRabbitMQ** integrates with RabbitMQ for actual messaging

**IntegrationEventLogEF** ensures reliability by persisting integration events, enabling retry mechanisms in case of failure

![image](https://github.com/user-attachments/assets/e4ca1f0e-28f3-4fe0-8fef-8ecf923a1460)

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

![image](https://github.com/user-attachments/assets/b4254887-f7b8-4b4a-9781-47f380c49c01)

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

![image](https://github.com/user-attachments/assets/d1dbad04-f184-454e-8df7-53f0d5a60d01)

### 2.2. Load the EventBusRabbitMQ project reference

![image](https://github.com/user-attachments/assets/eaf0b10c-9575-4239-8a4d-0f64af3a80e1)

### 2.3. ActivityExtensions

This code defines an extension method for the **Activity** class, which is used in distributed tracing to represent the execution of a single operation or span in a system

The **SetExceptionTags** method is a utility for **logging exception details** into an Activity

By tagging the **Activity** with exception information and marking it as an error, it helps observability tools (like OpenTelemetry) provide meaningful insights into application errors and failures

```csharp
using System.Diagnostics;

internal static class ActivityExtensions
{
    // See https://opentelemetry.io/docs/specs/otel/trace/semantic_conventions/exceptions/
    public static void SetExceptionTags(this Activity activity, Exception ex)
    {
        if (activity is null)
        {
            return;
        }

        activity.AddTag("exception.message", ex.Message);
        activity.AddTag("exception.stacktrace", ex.ToString());
        activity.AddTag("exception.type", ex.GetType().FullName);
        activity.SetStatus(ActivityStatusCode.Error);
    }
}
```

### 2.4. EventBusOptions

The EventBusOptions class defines two key options for an event bus that uses **RabbitMQ**

**SubscriptionClientName**: Identifies the client or consumer

**RetryCount**: Configures the number of retry attempts for operations

This class is part of the configuration framework, making it easy to customize and manage event bus behavior in a structured way

```csharp
namespace eShop.EventBusRabbitMQ;

public class EventBusOptions
{
    public string SubscriptionClientName { get; set; }
    public int RetryCount { get; set; } = 10;
}
```

### 2.5. RabbitMqDependencyInjectionExtensions

This code defines a set of extension methods for integrating a RabbitMQ-based event bus into a .NET application's dependency injection and hosting framework

The **AddRabbitMqEventBus** method simplifies the integration of a RabbitMQ-based event bus into a .NET application by:

- Registering RabbitMQ client and telemetry components

- Configuring the event bus options from the configuration file

- Registering the event bus as a hosted service to start consuming messages on application startup

- It follows the dependency injection and configuration patterns in .NET, ensuring modularity and maintainability

```csharp
using eShop.EventBusRabbitMQ;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

public static class RabbitMqDependencyInjectionExtensions
{
    // {
    //   "EventBus": {
    //     "SubscriptionClientName": "...",
    //     "RetryCount": 10
    //   }
    // }

    private const string SectionName = "EventBus";

    public static IEventBusBuilder AddRabbitMqEventBus(this IHostApplicationBuilder builder, string connectionName)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddRabbitMQClient(connectionName, configureConnectionFactory: factory =>
        {
            ((ConnectionFactory)factory).DispatchConsumersAsync = true;
        });

        // RabbitMQ.Client doesn't have built-in support for OpenTelemetry, so we need to add it ourselves
        builder.Services.AddOpenTelemetry()
           .WithTracing(tracing =>
           {
               tracing.AddSource(RabbitMQTelemetry.ActivitySourceName);
           });

        // Options support
        builder.Services.Configure<EventBusOptions>(builder.Configuration.GetSection(SectionName));

        // Abstractions on top of the core client API
        builder.Services.AddSingleton<RabbitMQTelemetry>();
        builder.Services.AddSingleton<IEventBus, RabbitMQEventBus>();
        // Start consuming messages as soon as the application starts
        builder.Services.AddSingleton<IHostedService>(sp => (RabbitMQEventBus)sp.GetRequiredService<IEventBus>());

        return new EventBusBuilder(builder.Services);
    }

    private class EventBusBuilder(IServiceCollection services) : IEventBusBuilder
    {
        public IServiceCollection Services => services;
    }
}
```


### 2.6. RabbitMQEventBus

This code defines a RabbitMQEventBus class, which implements an event bus using RabbitMQ for message-based communication in a distributed system

This class provides a robust RabbitMQ-based event bus with:

**Message Publishing and Consumption**: Handles event-based communication using RabbitMQ

**Resilience**: Implements retry logic for transient failures using Polly

**Distributed Tracing**: Integrates OpenTelemetry for observability

**Event Processing**: Manages event subscriptions and invokes appropriate handlers

**Best Practices**: Follows modern practices for distributed systems, such as telemetry, retries, and message acknowledgment

**Typical Workflow**:

**Application Startup**: The **StartAsync** method initializes the RabbitMQ connection and begins consuming messages

**Publishing Events**: **PublishAsync** serializes the event, enriches it with trace context, and publishes it to RabbitMQ

**Message Consumption**: Messages are consumed asynchronously, deserialized, and passed to appropriate event handlers

**Telemetry**: Activities are created for each operation (publish/consume) to provide distributed traceability

```csharp
namespace eShop.EventBusRabbitMQ;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Polly.Retry;

public sealed class RabbitMQEventBus(
    ILogger<RabbitMQEventBus> logger,
    IServiceProvider serviceProvider,
    IOptions<EventBusOptions> options,
    IOptions<EventBusSubscriptionInfo> subscriptionOptions,
    RabbitMQTelemetry rabbitMQTelemetry) : IEventBus, IDisposable, IHostedService
{
    private const string ExchangeName = "eshop_event_bus";

    private readonly ResiliencePipeline _pipeline = CreateResiliencePipeline(options.Value.RetryCount);
    private readonly TextMapPropagator _propagator = rabbitMQTelemetry.Propagator;
    private readonly ActivitySource _activitySource = rabbitMQTelemetry.ActivitySource;
    private readonly string _queueName = options.Value.SubscriptionClientName;
    private readonly EventBusSubscriptionInfo _subscriptionInfo = subscriptionOptions.Value;
    private IConnection _rabbitMQConnection;

    private IModel _consumerChannel;

    public Task PublishAsync(IntegrationEvent @event)
    {
        var routingKey = @event.GetType().Name;

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Creating RabbitMQ channel to publish event: {EventId} ({EventName})", @event.Id, routingKey);
        }

        using var channel = _rabbitMQConnection?.CreateModel() ?? throw new InvalidOperationException("RabbitMQ connection is not open");

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Declaring RabbitMQ exchange to publish event: {EventId}", @event.Id);
        }

        channel.ExchangeDeclare(exchange: ExchangeName, type: "direct");

        var body = SerializeMessage(@event);

        // Start an activity with a name following the semantic convention of the OpenTelemetry messaging specification.
        // https://github.com/open-telemetry/semantic-conventions/blob/main/docs/messaging/messaging-spans.md
        var activityName = $"{routingKey} publish";

        return _pipeline.Execute(() =>
        {
            using var activity = _activitySource.StartActivity(activityName, ActivityKind.Client);

            // Depending on Sampling (and whether a listener is registered or not), the activity above may not be created.
            // If it is created, then propagate its context. If it is not created, the propagate the Current context, if any.

            ActivityContext contextToInject = default;

            if (activity != null)
            {
                contextToInject = activity.Context;
            }
            else if (Activity.Current != null)
            {
                contextToInject = Activity.Current.Context;
            }

            var properties = channel.CreateBasicProperties();
            // persistent
            properties.DeliveryMode = 2;

            static void InjectTraceContextIntoBasicProperties(IBasicProperties props, string key, string value)
            {
                props.Headers ??= new Dictionary<string, object>();
                props.Headers[key] = value;
            }

            _propagator.Inject(new PropagationContext(contextToInject, Baggage.Current), properties, InjectTraceContextIntoBasicProperties);

            SetActivityContext(activity, routingKey, "publish");

            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace("Publishing event to RabbitMQ: {EventId}", @event.Id);
            }

            try
            {
                channel.BasicPublish(
                    exchange: ExchangeName,
                    routingKey: routingKey,
                    mandatory: true,
                    basicProperties: properties,
                    body: body);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                activity.SetExceptionTags(ex);

                throw;
            }
        });
    }

    private static void SetActivityContext(Activity activity, string routingKey, string operation)
    {
        if (activity is not null)
        {
            // These tags are added demonstrating the semantic conventions of the OpenTelemetry messaging specification
            // https://github.com/open-telemetry/semantic-conventions/blob/main/docs/messaging/messaging-spans.md
            activity.SetTag("messaging.system", "rabbitmq");
            activity.SetTag("messaging.destination_kind", "queue");
            activity.SetTag("messaging.operation", operation);
            activity.SetTag("messaging.destination.name", routingKey);
            activity.SetTag("messaging.rabbitmq.routing_key", routingKey);
        }
    }

    public void Dispose()
    {
        _consumerChannel?.Dispose();
    }

    private async Task OnMessageReceived(object sender, BasicDeliverEventArgs eventArgs)
    {
        static IEnumerable<string> ExtractTraceContextFromBasicProperties(IBasicProperties props, string key)
        {
            if (props.Headers.TryGetValue(key, out var value))
            {
                var bytes = value as byte[];
                return [Encoding.UTF8.GetString(bytes)];
            }
            return [];
        }

        // Extract the PropagationContext of the upstream parent from the message headers.
        var parentContext = _propagator.Extract(default, eventArgs.BasicProperties, ExtractTraceContextFromBasicProperties);
        Baggage.Current = parentContext.Baggage;

        // Start an activity with a name following the semantic convention of the OpenTelemetry messaging specification.
        // https://github.com/open-telemetry/semantic-conventions/blob/main/docs/messaging/messaging-spans.md
        var activityName = $"{eventArgs.RoutingKey} receive";

        using var activity = _activitySource.StartActivity(activityName, ActivityKind.Client, parentContext.ActivityContext);

        SetActivityContext(activity, eventArgs.RoutingKey, "receive");

        var eventName = eventArgs.RoutingKey;
        var message = Encoding.UTF8.GetString(eventArgs.Body.Span);

        try
        {
            activity?.SetTag("message", message);

            if (message.Contains("throw-fake-exception", StringComparison.InvariantCultureIgnoreCase))
            {
                throw new InvalidOperationException($"Fake exception requested: \"{message}\"");
            }

            await ProcessEvent(eventName, message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error Processing message \"{Message}\"", message);

            activity.SetExceptionTags(ex);
        }

        // Even on exception we take the message off the queue.
        // in a REAL WORLD app this should be handled with a Dead Letter Exchange (DLX). 
        // For more information see: https://www.rabbitmq.com/dlx.html
        _consumerChannel.BasicAck(eventArgs.DeliveryTag, multiple: false);
    }

    private async Task ProcessEvent(string eventName, string message)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Processing RabbitMQ event: {EventName}", eventName);
        }

        await using var scope = serviceProvider.CreateAsyncScope();

        if (!_subscriptionInfo.EventTypes.TryGetValue(eventName, out var eventType))
        {
            logger.LogWarning("Unable to resolve event type for event name {EventName}", eventName);
            return;
        }

        // Deserialize the event
        var integrationEvent = DeserializeMessage(message, eventType);
        
        // REVIEW: This could be done in parallel

        // Get all the handlers using the event type as the key
        foreach (var handler in scope.ServiceProvider.GetKeyedServices<IIntegrationEventHandler>(eventType))
        {
            await handler.Handle(integrationEvent);
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The 'JsonSerializer.IsReflectionEnabledByDefault' feature switch, which is set to false by default for trimmed .NET apps, ensures the JsonSerializer doesn't use Reflection.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "See above.")]
    private IntegrationEvent DeserializeMessage(string message, Type eventType)
    {
        return JsonSerializer.Deserialize(message, eventType, _subscriptionInfo.JsonSerializerOptions) as IntegrationEvent;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The 'JsonSerializer.IsReflectionEnabledByDefault' feature switch, which is set to false by default for trimmed .NET apps, ensures the JsonSerializer doesn't use Reflection.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "See above.")]
    private byte[] SerializeMessage(IntegrationEvent @event)
    {
        return JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), _subscriptionInfo.JsonSerializerOptions);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Messaging is async so we don't need to wait for it to complete. On top of this
        // the APIs are blocking, so we need to run this on a background thread.
        _ = Task.Factory.StartNew(() =>
        {
            try
            {
                logger.LogInformation("Starting RabbitMQ connection on a background thread");

                _rabbitMQConnection = serviceProvider.GetRequiredService<IConnection>();
                if (!_rabbitMQConnection.IsOpen)
                {
                    return;
                }

                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace("Creating RabbitMQ consumer channel");
                }

                _consumerChannel = _rabbitMQConnection.CreateModel();

                _consumerChannel.CallbackException += (sender, ea) =>
                {
                    logger.LogWarning(ea.Exception, "Error with RabbitMQ consumer channel");
                };

                _consumerChannel.ExchangeDeclare(exchange: ExchangeName,
                                        type: "direct");

                _consumerChannel.QueueDeclare(queue: _queueName,
                                     durable: true,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace("Starting RabbitMQ basic consume");
                }

                var consumer = new AsyncEventingBasicConsumer(_consumerChannel);

                consumer.Received += OnMessageReceived;

                _consumerChannel.BasicConsume(
                    queue: _queueName,
                    autoAck: false,
                    consumer: consumer);

                foreach (var (eventName, _) in _subscriptionInfo.EventTypes)
                {
                    _consumerChannel.QueueBind(
                        queue: _queueName,
                        exchange: ExchangeName,
                        routingKey: eventName);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error starting RabbitMQ connection");
            }
        },
        TaskCreationOptions.LongRunning);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static ResiliencePipeline CreateResiliencePipeline(int retryCount)
    {
        // See https://www.pollydocs.org/strategies/retry.html
        var retryOptions = new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<BrokerUnreachableException>().Handle<SocketException>(),
            MaxRetryAttempts = retryCount,
            DelayGenerator = (context) => ValueTask.FromResult(GenerateDelay(context.AttemptNumber))
        };

        return new ResiliencePipelineBuilder()
            .AddRetry(retryOptions)
            .Build();

        static TimeSpan? GenerateDelay(int attempt)
        {
            return TimeSpan.FromSeconds(Math.Pow(2, attempt));
        }
    }
}
```



### 2.7. RabbitMQTelemetry

The RabbitMQTelemetry class encapsulates distributed tracing and context propagation tools for RabbitMQ:

**ActivitySource**: Enables tracing operations like publishing or consuming messages

**Propagator**: Manages the propagation of trace context across service boundaries

It aligns with OpenTelemetry standards, making it easier to monitor and debug distributed systems

**Purpose in the Application**:

- Provides centralized tools for managing telemetry in RabbitMQ-based event-driven systems

- Facilitates distributed tracing with OpenTelemetry, enabling better observability of message flows

- Simplifies trace context management across services, ensuring that logs and telemetry data from different parts of the system are connected

```csharp
using System.Diagnostics;
using OpenTelemetry.Context.Propagation;

namespace eShop.EventBusRabbitMQ;

public class RabbitMQTelemetry
{
    public static string ActivitySourceName = "EventBusRabbitMQ";

    public ActivitySource ActivitySource { get; } = new(ActivitySourceName);
    public TextMapPropagator Propagator { get; } = Propagators.DefaultTextMapPropagator;
}
```

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

## 4. We Add RabbitMQ in the eShop.AppHost project

### 4.1. We Add the Nuget Package

![image](https://github.com/user-attachments/assets/afe12f75-0656-44cb-9bba-bd7173ab9755)

**Aspire.Hosting.RabbitMQ**: provides extension methods and resource definitions for integrating **RabbitMQ** servers into .NET Aspire applications

It enables developers to configure RabbitMQ resources within their application's hosting environment, facilitating seamless communication between services

**Key Features**:

Resource Configuration: Allows the addition of RabbitMQ server resources to the application model using the AddRabbitMQ method

Management Plugin Support: Enables the RabbitMQ management plugin for monitoring and management purposes through the WithManagementPlugin method

Data Persistence Options: Supports configuring data persistence using volumes or bind mounts to ensure data durability across container restarts

Health Checks: Automatically adds health checks to verify that the RabbitMQ server is running and that a connection can be established

### 4.2. We modify the eShop.AppHost middleware

We add the **RabbitMQ** container to the application model

This line of code configures a **RabbitMQ** server for the application, allowing it to send and receive messages for event-driven or distributed architectures

The name "eventbus" suggests that the **RabbitMQ** instance is intended to be used as an event bus for publishing and consuming events between services

```csharp
var rabbitMq = builder.AddRabbitMQ("eventbus");
```

A connection string from a source resource is injected as an environment variable into a destination resource

```csharp
var basketApi = builder.AddProject<Projects.Basket_API>("basket-api")
    .WithReference(redis)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithEnvironment("Identity__Url", identityEndpoint);

var catalogApi = builder.AddProject<Projects.Catalog_API>("catalog-api")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(catalogDb);

var webApp = builder.AddProject<Projects.WebApp>("webapp", launchProfileName)
    .WithExternalHttpEndpoints()
    .WithReference(basketApi)
    .WithReference(catalogApi)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithEnvironment("IdentityUrl", identityEndpoint);
```






