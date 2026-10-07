using System.Collections.Concurrent;
using System.Reflection;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace GrowthTracker.Application;

/// <summary>
/// Resolves the handler registered for a command/query inside a new scope and runs it.
/// Entity invariant violations (<see cref="DomainException"/>) become failed results so the UI can show them.
/// </summary>
internal sealed class UseCaseDispatcher(IServiceScopeFactory scopeFactory) : IUseCaseDispatcher
{
    private static readonly ConcurrentDictionary<(Type, Type, bool), (Type HandlerType, MethodInfo Handle)> Cache = new();

    public async Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        try
        {
            return await InvokeAsync<Result<TResult>>(command, typeof(ICommandHandler<,>), typeof(TResult), cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<TResult>.Failure(ex.Code, ex.Message);
        }
    }

    public Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default) =>
        InvokeAsync<TResult>(query, typeof(IQueryHandler<,>), typeof(TResult), cancellationToken);

    private async Task<T> InvokeAsync<T>(object request, Type openHandler, Type resultType, CancellationToken cancellationToken)
    {
        var (handlerType, handle) = Cache.GetOrAdd((request.GetType(), resultType, openHandler == typeof(ICommandHandler<,>)), key =>
        {
            var type = openHandler.MakeGenericType(key.Item1, key.Item2);
            return (type, type.GetMethod("HandleAsync")!);
        });

        await using var scope = scopeFactory.CreateAsyncScope();
        object handler = scope.ServiceProvider.GetRequiredService(handlerType);
        try
        {
            return await (Task<T>)handle.Invoke(handler, [request, cancellationToken])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(ex.InnerException);
            throw;
        }
    }
}
