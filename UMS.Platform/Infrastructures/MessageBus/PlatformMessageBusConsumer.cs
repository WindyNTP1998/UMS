#nullable enable
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Common.Utils;

namespace UMS.Platform.Infrastructures.MessageBus;

public interface IPlatformMessageBusConsumer
{
    /// <summary>
    ///     Main Entry Handle Method
    /// </summary>
    Task HandleAsync(object message, string routingKey);

    /// <summary>
    ///     Main handle logic only method of the consumer
    /// </summary>
    Task HandleLogicAsync(object message, string routingKey);

    /// <summary>
    ///     Config the time in milliseconds to log warning if the process consumer time is over ProcessWarningTimeMilliseconds.
    /// </summary>
    long? SlowProcessWarningTimeMilliseconds();

    bool DisableSlowProcessWarning();

    JsonSerializerOptions? CustomJsonSerializerOptions();

    /// <summary>
    ///     Default is 0. Return bigger number order to execute it later by order ascending
    /// </summary>
    int ExecuteOrder();

    public static PlatformBusMessageRoutingKey BuildForConsumerDefaultBindingRoutingKey(Type consumerType)
    {
        var messageType = GetConsumerMessageType(consumerType);

        return PlatformBusMessageRoutingKey.BuildDefaultRoutingKey(messageType);
    }

    public static Type GetConsumerMessageType(Type consumerGenericType)
    {
        return consumerGenericType.GetGenericArguments()[0];
    }

    public static void LogError<TMessage>(ILogger logger,
        Type consumerType,
        TMessage message,
        string routingKey,
        Exception e)
        where TMessage : class, new()
    {
        logger.LogError(e,
            "Error Consume message bus. [ConsumerType:{ConsumerType}]; [MessageType:{MessageType}]; [RoutingKey:{RoutingKey}]; [MessageContent:{MessageContent}]; ",
            consumerType.FullName,
            message.GetType().GetNameOrGenericTypeName(),
            routingKey,
            message.ToJson());
    }
}

public interface IPlatformMessageBusConsumer<in TMessage> : IPlatformMessageBusConsumer
    where TMessage : class, new()
{
    /// <summary>
    ///     Main Entry Handle Method
    /// </summary>
    Task HandleAsync(TMessage message, string routingKey);

    /// <summary>
    ///     Main handle logic only method of the consumer
    /// </summary>
    Task HandleLogicAsync(TMessage message, string routingKey);
}

public abstract class PlatformMessageBusConsumer : IPlatformMessageBusConsumer
{
    public abstract Task HandleAsync(object message, string routingKey);

    public abstract Task HandleLogicAsync(object message, string routingKey);

    public virtual long? SlowProcessWarningTimeMilliseconds()
    {
        return PlatformMessageBusConfig.DefaultProcessWarningTimeMilliseconds;
    }

    public virtual bool DisableSlowProcessWarning()
    {
        return false;
    }

    public virtual JsonSerializerOptions? CustomJsonSerializerOptions()
    {
        return null;
    }

    public virtual int ExecuteOrder()
    {
        return 0;
    }

    /// <summary>
    ///     Get <see cref="PlatformBusMessage{TPayload}" /> concrete message type from a
    ///     <see cref="IPlatformMessageBusConsumer" /> consumer
    ///     <br />
    ///     Get a generic type: PlatformEventBusMessage{TMessage} where TMessage = TMessagePayload
    ///     of IPlatformEventBusConsumer{TMessagePayload}
    /// </summary>
    public static Type GetConsumerMessageType(IPlatformMessageBusConsumer consumer)
    {
        var consumerGenericType = consumer
                                      .GetType()
                                      .GetInterfaces()
                                      .FirstOrDefault(x =>
                                          x.IsGenericType && x.GetGenericTypeDefinition() ==
                                          typeof(IPlatformMessageBusConsumer<>)) ??
                                  throw new Exception("Must be implementation of IPlatformMessageBusConsumer<>");

        return IPlatformMessageBusConsumer.GetConsumerMessageType(consumerGenericType);
    }

    public static async Task InvokeConsumerAsync(IPlatformMessageBusConsumer consumer,
        object busMessage,
        string routingKey,
        IPlatformMessageBusConfig messageBusConfig,
        ILogger? logger = null)
    {
        logger?.LogInformation(
            "[MessageBus] Start invoking consumer. Name: {ConsumerName}. RoutingKey: {RoutingKey}. TrackingId: {TrackingId}",
            consumer.GetType().FullName,
            routingKey,
            busMessage.As<IPlatformTrackableBusMessage>()?.TrackingId ?? "n/a");

        if (messageBusConfig.EnableLogConsumerProcessTime && !consumer.DisableSlowProcessWarning())
            await Util.TaskRunner.ProfileExecutionAsync(() => DoInvokeConsumer(consumer, busMessage, routingKey),
                elapsedMilliseconds =>
                {
                    var logMessage =
                        $"ElapsedMilliseconds:{elapsedMilliseconds}. Consumer:{consumer.GetType().FullName}. RoutingKey:{routingKey}. TrackingId:{busMessage.As<IPlatformTrackableBusMessage>()?.TrackingId ?? "n/a"}.";

                    var toCheckSlowProcessWarningTimeMilliseconds = consumer.SlowProcessWarningTimeMilliseconds() ??
                                                                    messageBusConfig
                                                                        .LogSlowProcessWarningTimeMilliseconds;
                    if (elapsedMilliseconds >= toCheckSlowProcessWarningTimeMilliseconds)
                        logger?.LogWarning(
                            "[MessageBus] SlowProcessWarningTimeMilliseconds:{SlowProcessWarningTimeMilliseconds}. ElapsedMilliseconds:{ElapsedMilliseconds}. {LogMessage}. MessageContent: {BusMessage}",
                            toCheckSlowProcessWarningTimeMilliseconds,
                            elapsedMilliseconds,
                            logMessage,
                            busMessage.ToJson());
                });
        else
            await DoInvokeConsumer(consumer,
                busMessage,
                routingKey);

        logger?.LogInformation(
            "[MessageBus] Finished invoking consumer. Name: {ConsumerName}. RoutingKey: {RoutingKey}. TrackingId: {TrackingId}",
            consumer.GetType().FullName,
            routingKey,
            busMessage.As<IPlatformTrackableBusMessage>()?.TrackingId ?? "n/a");
    }

    private static async Task DoInvokeConsumer(IPlatformMessageBusConsumer consumer,
        object eventBusMessage,
        string routingKey)
    {
        try
        {
            await consumer.HandleAsync(eventBusMessage, routingKey);
        }
        catch (Exception e)
        {
            throw new PlatformInvokeConsumerException(e, consumer.GetType().FullName, eventBusMessage);
        }
    }
}

public abstract class PlatformMessageBusConsumer<TMessage>
    : PlatformMessageBusConsumer, IPlatformMessageBusConsumer<TMessage>
    where TMessage : class, new()
{
    protected readonly ILogger Logger;
    protected readonly ILoggerFactory LoggerFactory;

    public PlatformMessageBusConsumer(ILoggerFactory loggerFactory)
    {
        LoggerFactory = loggerFactory;
        Logger = CreateLogger(loggerFactory);
    }

    public virtual int RetryOnFailedTimes => 5;

    public virtual double RetryOnFailedDelaySeconds => 0.5;

    public override Task HandleAsync(object message, string routingKey)
    {
        return HandleAsync(message.Cast<TMessage>(), routingKey);
    }

    public override Task HandleLogicAsync(object message, string routingKey)
    {
        return HandleLogicAsync(message.Cast<TMessage>(), routingKey);
    }

    public virtual async Task HandleAsync(TMessage message, string routingKey)
    {
        if (!HandleWhen(message, routingKey)) return;

        try
        {
            if (RetryOnFailedTimes > 0)
                // Retry RetryOnFailedTimes to help resilient consumer. Sometime parallel, create/update concurrency could lead to error
                await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                    () => ExecuteHandleLogicAsync(message, routingKey),
                    retryCount: RetryOnFailedTimes,
                    sleepDurationProvider: retryAttempt => RetryOnFailedDelaySeconds.Seconds());
            else
                await ExecuteHandleLogicAsync(message, routingKey);
        }
        catch (Exception e)
        {
            IPlatformMessageBusConsumer.LogError(Logger, GetType(), message,
                routingKey, e);
            throw;
        }
        finally
        {
            Util.GarbageCollector.Collect(immediately: true);
        }
    }

    public abstract Task HandleLogicAsync(TMessage message, string routingKey);

    protected virtual Task ExecuteHandleLogicAsync(TMessage message, string routingKey)
    {
        return HandleLogicAsync(message, routingKey);
    }

    public virtual bool HandleWhen(TMessage message, string routingKey)
    {
        return true;
    }

    public static ILogger CreateLogger(ILoggerFactory loggerFactory)
    {
        return loggerFactory.CreateLogger(typeof(PlatformMessageBusConsumer));
    }

    public ILogger CreateLogger()
    {
        return CreateLogger(LoggerFactory);
    }
}