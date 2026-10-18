using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Configuration;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.Infrastructure.Services;
using SupportDesk.Domain.Aggregates.Tickets;
using System.Linq;

namespace SupportDesk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. See README.md for local setup.");

        services.AddDbContext<SupportDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        // Bind and validate at startup: all four priorities must be present and positive.
        services
            .Configure<SlaOptions>(configuration.GetSection("Sla"))
            .AddOptions<SlaOptions>()
            .Bind(configuration.GetSection("Sla"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<SupportDbContext>());

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ICredentialHasher, IdentityCredentialHasher>();

        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        services.AddScoped<ITicketQueries, TicketQueries>();
        services.AddScoped<ICustomerQueries, CustomerQueries>();
        services.AddScoped<IAgentQueries, AgentQueries>();
        services.AddScoped<ICategoryQueries, CategoryQueries>();

        services.AddScoped<SupportDbSeeder>();

        return services;
    }
}