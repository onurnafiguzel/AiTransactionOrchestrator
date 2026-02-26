using BuildingBlocks.Contracts.Resiliency;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Transaction.Application.Abstractions;
using Transaction.Application.Outbox;
using Transaction.Application.Users;
using Transaction.Infrastructure.Authentication;
using Transaction.Infrastructure.Inbox;
using Transaction.Infrastructure.Outbox;
using Transaction.Infrastructure.Persistence.Audit;
using Transaction.Infrastructure.Persistence;
using Transaction.Infrastructure.Repositories;

namespace Transaction.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Transaction Infrastructure servislerini IServiceCollection'a kaydeder
    /// DbContext, Interceptor'lar, Repository'ler ve diğer infrastructure bağımlılıklarını yapılandırır
    /// </summary>
    /// <param name="services">IServiceCollection instance'ı</param>
    /// <param name="connectionString">PostgreSQL connection string'i</param>
    /// <returns>IServiceCollection - fluent API için method chaining desteği</returns>
    public static IServiceCollection AddTransactionInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        // IHttpContextAccessor'ı register et (Audit Interceptor'ın kullanması için)
        // Bu servis, current HTTP request'in context'ine erişim sağlar
        // Endpoint bilgisi ve kullanıcı bilgisini almak için gereklidir
        services.AddHttpContextAccessor();

        // DbContext'i register et - DomainEventDispatcher ve Audit Interceptor'larını kurut
        services.AddScoped(sp =>
        {
            var options = new DbContextOptionsBuilder<TransactionDbContext>()
                .UseNpgsql(connectionString, npgsqlOptions =>
                {
                    // Geçici hataları otomatik olarak tekrar dene (network timeout, deadlock vb.)
                    // Bu sayede uygulamanın dayanıklılığı artar
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);

                    // Veritabanı komutları için maximum timeout süresi (30 saniye)
                    npgsqlOptions.CommandTimeout(30);
                })
                .Options;

            // DI container'dan IMediator ve IHttpContextAccessor'ı al
            var mediator = sp.GetService<IMediator>();
            var httpContextAccessor = sp.GetService<IHttpContextAccessor>();

            // DbContext instance'ını oluştur ve interceptor'ları konfigure et
            // - DomainEventDispatcher: Aggregate root'lardan domain events dispatch eder
            // - Audit Interceptor: Tüm veritabanı değişikliklerini audit log tablosuna kaydeder
            return new TransactionDbContext(options, mediator, httpContextAccessor);
        });

        // Repository patterns
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IUserRepository, Transaction.Infrastructure.Persistence.Repositories.UserRepository>();

        // Outbox pattern implementation (transactional outbox)
        services.AddScoped<IOutboxWriter, EfCoreOutboxWriter>();

        // Unit of Work pattern
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        // Inbox pattern guard (idempotence için)
        services.AddScoped<InboxGuard>();

        // Audit query helper
        services.AddScoped<AuditLogQueryHelper>();

        // Authentication services
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        // Resilience pipelines (Polly) for retry policies, circuit breaker vb.
        services.AddResiliencePipelines();

        return services;
    }
}
