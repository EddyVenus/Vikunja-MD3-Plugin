using Microsoft.Extensions.DependencyInjection;

namespace VikunjaPlugin.Api;

public static class VikunjaRegistration
{
	public static IServiceCollection AddVikunjaApi(this IServiceCollection services)
	{
		services.AddSingleton<VikunjaCredentials>();
		services.AddHttpClient<VikunjaClient>(client => client.Timeout = TimeSpan.FromSeconds(10));
		return services;
	}
}
