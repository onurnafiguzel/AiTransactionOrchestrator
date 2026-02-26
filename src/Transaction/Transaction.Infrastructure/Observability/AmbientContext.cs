using System;
using System.Threading;

namespace Transaction.Infrastructure.Observability;

/// <summary>
/// HTTP context dışında (worker, consumer, background jobs) 
/// audit bilgilerini (source, user) taşımak için kullanılan AsyncLocal context'ler
/// </summary>
public static class AmbientContext
{
    /// <summary>
    /// Event/job adı veya tetikleyen kaynağı tutar
    /// Örn: "event:TransactionApproved", "job:DailyReconciliation"
    /// </summary>
    public static class AuditSource
    {
        private static readonly AsyncLocal<string?> CurrentSource = new();

        public static string? Current => CurrentSource.Value;

        public static IDisposable Use(string source)
        {
            var prior = CurrentSource.Value;
            CurrentSource.Value = source;
            return new ResetScope(prior);
        }

        private sealed class ResetScope : IDisposable
        {
            private readonly string? _prior;
            private bool _disposed;

            public ResetScope(string? prior)
            {
                _prior = prior;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                CurrentSource.Value = _prior;
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// İşlemi gerçekleştiren kullanıcı ID'sini tutar
    /// HTTP context yoksa bu değer kullanılır
    /// </summary>
    public static class AuditUser
    {
        private static readonly AsyncLocal<string?> CurrentUser = new();

        public static string? Current => CurrentUser.Value;

        public static IDisposable Use(string? userId)
        {
            var prior = CurrentUser.Value;
            CurrentUser.Value = userId;
            return new ResetScope(prior);
        }

        private sealed class ResetScope : IDisposable
        {
            private readonly string? _prior;
            private bool _disposed;

            public ResetScope(string? prior)
            {
                _prior = prior;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                CurrentUser.Value = _prior;
                _disposed = true;
            }
        }
    }
}
