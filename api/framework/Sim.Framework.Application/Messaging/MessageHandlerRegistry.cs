using Microsoft.Extensions.DependencyInjection;
using Sim.Framework.Domain.Shared.Messaging;

namespace Sim.Framework.Application.Messaging;

public sealed record MessageHandlerRegistration(
    string MessageType,
    Type PayloadType,
    Type HandlerType);

public sealed class MessageHandlerRegistry
{
    private readonly Dictionary<string, List<MessageHandlerRegistration>> _registrations =
        new(StringComparer.Ordinal);

    public MessageHandlerRegistry() : this([])
    {
    }

    public MessageHandlerRegistry(IEnumerable<MessageHandlerRegistration> registrations)
    {
        foreach (var registration in registrations)
        {
            Add(registration);
        }
    }

    public void Add(MessageHandlerRegistration registration)
    {
        if (!_registrations.TryGetValue(registration.MessageType, out var handlers))
        {
            handlers = [];
            _registrations[registration.MessageType] = handlers;
        }

        if (handlers.All(item => item.HandlerType != registration.HandlerType))
        {
            handlers.Add(registration);
        }
    }

    public IReadOnlyList<MessageHandlerRegistration> Get(string messageType) =>
        _registrations.TryGetValue(messageType, out var handlers) ? handlers : [];
}

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessageHandler<TMessage, THandler>(
        this IServiceCollection services,
        string messageType)
        where THandler : class, IMessageHandler<TMessage>
    {
        services.AddScoped<THandler>();
        services.AddSingleton(new MessageHandlerRegistration(
            messageType,
            typeof(TMessage),
            typeof(THandler)));
        return services;
    }
}
