using BAL;
using BAL.Interface;
using DAL;
using DAL.CrudOperations;
using DAL.Interface;
using GenxAi_Solutions.Models.Security;
using GenxAi_Solutions.Services;
using GenxAi_Solutions.Services.Background;
using GenxAi_Solutions.Services.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;



namespace GenxAi_Solutions.Utils
{
    public static class DepandancyInjectionRegister
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
            services.AddSingleton<IJwtTokenService, JwtTokenService>();

            //// Register your DAL services here
            services.AddSingleton<ICRUDOperations>(x => new CRUDOperations(x.GetService<IConfiguration>()!, connectionString!));
            services.AddScoped<IScreen, Screen>();
            services.AddScoped<ICompanyProfileDAL, CompanyProfileDAL>();
            services.AddScoped<IUserGroup, UserGroup>();
            services.AddScoped<IUserMaster, UserMasterDAL>();
            services.AddScoped<IChatBotDAL, ChatBotDAL>();

            //// Register your BAL services here
            services.AddScoped<IScreen_BAL, ScreenBAL>();
            services.AddScoped<ICompanyProfileBAL, CompanyProfileBAL>();
            services.AddScoped<IUserGroup_BAL, UserGroup_BAL>();
            services.AddScoped<IUserMaster_BAL, UserMaster_BAL>();
            services.AddScoped<IChatBotBAL, ChatBotBAL>();

            
            ///Register your Other services here

            services.AddHttpClient("openai-resilient")
                .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    return new SocketsHttpHandler
                    {
                        // Refresh pooled connections to respect DNS changes and avoid stale sockets
                        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                        ConnectTimeout = TimeSpan.FromSeconds(10),
                        KeepAlivePingDelay = TimeSpan.FromSeconds(30),
                        KeepAlivePingTimeout = TimeSpan.FromSeconds(20),
                        KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,
                        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                    };
                })
                .AddHttpMessageHandler(() => new DnsAndTransientRetryHandler());
            
            services.AddSingleton<Kernel>();

            //services.AddOpenAITextEmbeddingGeneration(
            //    serviceId: "openai-embed",
            //    modelId: "text-embedding-3-large",                 // or your model
            //    apiKey: configuration["OpenAI:ApiKey"]      // put your key in config/secret
            //);
            services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("openai-resilient");

                var kernelBuilder = Kernel.CreateBuilder();
                kernelBuilder.AddOpenAITextEmbeddingGeneration(
                    serviceId: configuration["OpenAI:EmbederServiceId"],
                    modelId: configuration["OpenAI:EmbederModelId"],
                    apiKey: configuration["OpenAI:ApiKey"]
                );
                kernelBuilder.Services.AddOpenAIChatCompletion(configuration["OpenAI:ChatModelId"], configuration["OpenAI:ApiKey"]);
                
#pragma warning disable SKEXP0010
                // embedding generator
                kernelBuilder.Services.AddOpenAIEmbeddingGenerator(configuration["OpenAI:EmbederModelId"], configuration["OpenAI:ApiKey"]);
#pragma warning restore SKEXP0010
                return kernelBuilder.Build();
            });

    //        services.AddHttpClient("openai-proxy")
    //.ConfigurePrimaryHttpMessageHandler(sp =>
    //{
    //    var cfg = sp.GetRequiredService<IConfiguration>();
    //    var useProxy = cfg.GetValue<bool>("Networking:UseProxy");
    //    var proxyUrl = cfg.GetValue<string>("Networking:ProxyUrl");

    //    var handler = new HttpClientHandler();
    //    if (useProxy && !string.IsNullOrWhiteSpace(proxyUrl))
    //    {
    //        handler.UseProxy = true;
    //        handler.Proxy = new WebProxy(proxyUrl) { BypassProxyOnLocal = true };
    //    }
    //    return handler;
    //});

    //        // Build Kernel yourself and register it
    //        services.AddSingleton<Kernel>(sp =>
    //        {
    //            var cfg = sp.GetRequiredService<IConfiguration>();
    //            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("openai-proxy");

    //            var apiKey = cfg["OpenAI:ApiKey"];
    //            var embModel = cfg["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
    //            var chatModel = cfg["OpenAI:ChatModel"] ?? "gpt-4o-mini";

    //            var kb = Kernel.CreateBuilder();
    //            kb.AddOpenAITextEmbeddingGeneration("openai-embed", embModel, apiKey, httpClient: http);
    //            kb.AddOpenAIChatCompletion("openai-chat", chatModel, apiKey, httpClient: http);
    //            return kb.Build();
    //        });


            services.AddSignalR();
            services.AddSingleton<IBackgroundJobQueue, BackgroundJobQueue>();
            services.AddSingleton<IJobStore, InMemoryJobStore>();
            services.AddHostedService<QueuedWorker>();
            services.AddScoped<IVectorStoreFactory, VectorStoreFactory>();
            services.AddScoped<IVectorStoreSeedService, VectorStoreSeedService>();
            //services.AddScoped<ISqlConfigRepository, SqlConfigRepository>();
            services.AddSingleton<ISqlConfigRepository, SqlConfigRepository>();//suchita
            services.AddScoped<ISemanticSeeder, SemanticSeeder>();
            services.AddSingleton<INotificationStore, NotificationStore>(); // ADO.NET implementation
            services.AddSingleton<Notifier>();  // optional helper to insert+push

            // Register audit logger service
            services.AddSingleton<IAuditLogger, AuditLogger>();


            // 10-second background broadcaster (optional; keep if you want a safety net)
            //services.AddHostedService<NotificationWatcher>();

            return services;
        }
    }
}
