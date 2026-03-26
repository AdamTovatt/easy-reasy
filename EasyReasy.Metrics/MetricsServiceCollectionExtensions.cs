using EasyReasy.Metrics.Collection;
using EasyReasy.Metrics.Configuration;
using EasyReasy.Metrics.Query;
using EasyReasy.Metrics.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


namespace EasyReasy.Metrics
{
    /// <summary>
    /// Extension methods for registering EasyReasy.Metrics services in the dependency injection container.
    /// </summary>
    public static class MetricsServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all EasyReasy.Metrics services: the snapshot repository, collection service,
        /// query service, hosted background service, and configuration options.
        /// Requires <c>DbDataSource</c> and <c>IDbSessionFactory</c> from EasyReasy.Database
        /// to be registered in the service collection before resolving metrics services.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configureOptions">
        /// An optional action to configure <see cref="EasyReasyMetricsOptions"/>.
        /// If <c>null</c>, default options are used.
        /// </param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddEasyReasyMetrics(
            this IServiceCollection services,
            Action<EasyReasyMetricsOptions>? configureOptions = null)
        {
            EasyReasyMetricsOptions options = new EasyReasyMetricsOptions();
            configureOptions?.Invoke(options);
            options.Validate();

            services
                .AddOptions<EasyReasyMetricsOptions>()
                .Configure(configureOptions ?? (_ => { }));

            services.AddScoped<IMetricSnapshotRepository, MetricSnapshotRepository>();
            services.AddScoped<IMetricCollectionService, MetricCollectionService>();
            services.AddScoped<IMetricQueryService, MetricQueryService>();
            services.AddHostedService<MetricCollectionHostedService>();

            return services;
        }

        /// <summary>
        /// Registers a metric collector implementation in the dependency injection container.
        /// The collector is registered with a scoped lifetime as <see cref="IMetricCollector"/>,
        /// since collectors may depend on scoped services such as repositories.
        /// </summary>
        /// <typeparam name="T">The concrete collector type implementing <see cref="IMetricCollector"/>.</typeparam>
        /// <param name="services">The service collection to add the collector to.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddMetricCollector<T>(this IServiceCollection services)
            where T : class, IMetricCollector
        {
            services.AddScoped<IMetricCollector, T>();
            return services;
        }
    }
}
