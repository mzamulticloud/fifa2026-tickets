using Fifa2026.V2.Functions.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// .NET 8 isolated worker entrypoint.
// ConfigureFunctionsWebApplication enables ASP.NET Core integration
// (HttpRequest/IActionResult) in HTTP triggers.
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        // Application Insights — distributed tracing (W3C Trace Context, ADE-000 Inv 5).
        // The Activity API is fed automatically by the Functions/ServiceBus SDKs.
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // SQL purchase repository (parameterized queries via Dapper + Microsoft.Data.SqlClient).
        services.AddSingleton<IPurchaseRepository, PurchaseRepository>();

        // Story 2.4 (F4) — webhook fire-and-forget ao n8n após gravar a compra.
        // Typed HttpClient via IHttpClientFactory (gestão correta de sockets/pooling).
        // O timeout efetivo (5s) é controlado pelo próprio notifier via CancellationToken
        // encadeado; mantemos o HttpClient.Timeout como guarda superior conservador.
        services.AddHttpClient<IN8nWebhookNotifier, N8nWebhookNotifier>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
    })
    .ConfigureLogging(logging =>
    {
        // GOTCHA do isolated worker: AddApplicationInsightsTelemetryWorkerService aplica uma
        // regra de filtro PADRÃO que limita o provider do Application Insights a Warning,
        // descartando os logs Information do worker (nossos ILogger.LogInformation com
        // BeginScope de OrderId/correlationId) ANTES de chegarem ao App Insights. O host.json
        // controla o HOST, não esse filtro do worker — por isso os logs de aplicação não
        // apareciam. Removemos a regra para que a telemetria de aplicação (rastreio ponta-a-
        // ponta por correlationId/orderId) chegue ao App Insights.
        logging.Services.Configure<LoggerFilterOptions>(options =>
        {
            var aiRule = options.Rules.FirstOrDefault(rule =>
                rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
            if (aiRule is not null)
            {
                options.Rules.Remove(aiRule);
            }
        });
    })
    .Build();

host.Run();
