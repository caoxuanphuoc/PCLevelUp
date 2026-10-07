using GrowthTracker.Domain.Common;

namespace GrowthTracker.Application.Abstractions;

/// <summary>A write use case returning <typeparamref name="TResult"/> on success.</summary>
public interface ICommand<TResult>;

/// <summary>A read use case returning a DTO built for one screen.</summary>
public interface IQuery<TResult>;

public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// Entry point for ViewModels. Each call runs in its own DI scope, so every use case gets a fresh DbContext.
/// </summary>
public interface IUseCaseDispatcher
{
    Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);

    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}

/// <summary>Shared error codes for failures that are not entity invariants.</summary>
public static class Errors
{
    public static Error NotFound(string what) => new($"{what}.not_found", $"{what} was not found.");
}
