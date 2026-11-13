using Microsoft.Extensions.DependencyInjection;

namespace VikunjaPlugin.Api;

public static class VikunjaRegistration
{
	public static IServiceCollection AddVikunjaApi(this IServiceCollection services)
	{
		services.AddSingleton<VikunjaCredentials>();
		// The client lives in a singleton, so connections are recycled here to pick up DNS changes.
		services.AddHttpClient<VikunjaClient>(client => client.Timeout = TimeSpan.FromSeconds(10))
			.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) });
		return services;
	}
}
