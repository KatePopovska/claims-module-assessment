using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using NSubstitute;

namespace ClaimsModule.Application.UnitTests;

internal static class AsyncQueryable
{
    public static DbSet<T> DbSetOf<T>(params T[] items) where T : class
    {
        IQueryable<T> queryable = new TestAsyncEnumerable<T>(items);
        var set = Substitute.For<DbSet<T>, IQueryable<T>, IAsyncEnumerable<T>>();

        ((IQueryable<T>)set).Provider.Returns(queryable.Provider);
        ((IQueryable<T>)set).Expression.Returns(queryable.Expression);
        ((IQueryable<T>)set).ElementType.Returns(queryable.ElementType);
        ((IAsyncEnumerable<T>)set).GetAsyncEnumerator(Arg.Any<CancellationToken>()).Returns(_ => ((IAsyncEnumerable<T>)queryable).GetAsyncEnumerator());

        return set;
    }

    private sealed class TestAsyncQueryProvider<T>(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<T>(expression);

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression);

        public object? Execute(Expression expression) => inner.Execute(expression);

        public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var resultType = typeof(TResult).GetGenericArguments()[0];
            var result = typeof(IQueryProvider).GetMethods()
                .Single(m => m.Name == nameof(IQueryProvider.Execute) && m.IsGenericMethod)
                .MakeGenericMethod(resultType)
                .Invoke(inner, [expression]);

            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, [result])!;
        }
    }

    private sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> items) : base(items) { }

        public TestAsyncEnumerable(Expression expression) : base(expression) { }

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => new TestAsyncEnumerator(this.AsEnumerable().GetEnumerator());

        private sealed class TestAsyncEnumerator(IEnumerator<T> inner) : IAsyncEnumerator<T>
        {
            public T Current => inner.Current;

            public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());

            public ValueTask DisposeAsync()
            {
                inner.Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
