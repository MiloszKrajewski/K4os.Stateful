// ReSharper disable UnusedParameter.Local

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

var app = Host
	.CreateDefaultBuilder(args)
	.UseSerilog(
		(context, configuration) => {
			var serilogTemplate =
				"[{Timestamp:HH:mm:ss.fff} {Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}";
			configuration
				.ReadFrom.Configuration(context.Configuration)
				.Enrich.FromLogContext()
				.WriteTo.Console(outputTemplate: serilogTemplate);
		})
	.Build();

await app.StartAsync();

await Execute(
	app.Services,
	app.Services.GetRequiredService<ILoggerFactory>(),
	app.Services.GetRequiredService<IConfiguration>());

await app.StopAsync();
return;

//--------------------------------------------------------------------------------------------------

static async Task Execute(
	IServiceProvider serviceProvider,
	ILoggerFactory loggerFactory,
	IConfiguration configuration)
{
	await Task.CompletedTask;
	var log = loggerFactory.CreateLogger("root");

	log.LogTrace("Trace...");
	log.LogDebug("Debug...");
	log.LogInformation("Info...");
	log.LogWarning("Warning...");
	log.LogError("Error...");
	log.LogCritical("Critical...");

	await Task.WhenAny(
		Task.Run(() => Console.In.ReadLineAsync()),
		Task.Delay(TimeSpan.FromSeconds(5)));
}
