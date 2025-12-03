using BAL;
using BAL.Interface;
using DAL;
using DAL.CrudOperations;
using DAL.Interface;
using GenxAi_Solutions_V1.Models.Security;
using GenxAi_Solutions_V1.Services;
using GenxAi_Solutions_V1.Services.Background;
using GenxAi_Solutions_V1.Services.Interfaces;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using OpenAI.Chat;
using OpenAI.Embeddings;
using System;
using System.Security.Policy;
using static Azure.Core.HttpHeader;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static UglyToad.PdfPig.Core.PdfSubpath;



namespace GenxAi_Solutions_V1.Utils
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

            // IChatClient
            services.AddSingleton<IChatClient>(sp =>
            {
                if (string.IsNullOrWhiteSpace(configuration["OpenAI:ApiKey"]))
                    throw new InvalidOperationException("OpenAI:ApiKey is not configured.");

                var chatClient = new ChatClient(configuration["OpenAI:ChatModelId"], configuration["OpenAI:ApiKey"]);
                return chatClient.AsIChatClient();
            });

            // IEmbeddingGenerator
           services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
            {
                if (string.IsNullOrWhiteSpace(configuration["OpenAI:ApiKey"]))
                    throw new InvalidOperationException("OpenAI:ApiKey is not configured.");

                var embeddingClient = new EmbeddingClient(configuration["OpenAI:EmbederModelId"], configuration["OpenAI:ApiKey"]);
                return embeddingClient.AsIEmbeddingGenerator(1536);
            });

            //services.AddOpenAITextEmbeddingGeneration(
            //    serviceId: "openai-embed",
            //    modelId: "text-embedding-3-large",                 // or your model
            //    apiKey: configuration["OpenAI:ApiKey"]      // put your key in config/secret
            //);
//            services.AddSingleton(sp =>
//            {
//                var config = sp.GetRequiredService<IConfiguration>();
//                var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("openai-resilient");

//                var kernelBuilder = Kernel.CreateBuilder();
//                kernelBuilder.AddOpenAITextEmbeddingGeneration(
//                    serviceId: configuration["OpenAI:EmbederServiceId"],
//                    modelId: configuration["OpenAI:EmbederModelId"],
//                    apiKey: configuration["OpenAI:ApiKey"]
//                );
//                kernelBuilder.Services.AddOpenAIChatCompletion(configuration["OpenAI:ChatModelId"], configuration["OpenAI:ApiKey"]);

//#pragma warning disable SKEXP0010
//                // embedding generator
//                kernelBuilder.Services.AddOpenAIEmbeddingGenerator(configuration["OpenAI:EmbederModelId"], configuration["OpenAI:ApiKey"]);
//#pragma warning restore SKEXP0010
//                return kernelBuilder.Build();
//            });

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




            // Minimal in-memory chat history (drop-in, no persistence change to your DBs)
            services.AddSingleton<IChatHistoryService, InMemoryChatHistoryService>();
            //services.AddSingleton<ISqlChatService, SqlChatService>();
            // services.AddSingleton<IPdfChatService, PdfChatService>();

            

            services.AddSingleton(sp => {
                var chatClient = sp.GetRequiredService<IChatClient>();
                return new ChatClientAgent(
                    chatClient,
                    new ChatClientAgentOptions
                    {
                        Name = "SqlQueryAgent",
                        Instructions =
                            "You are a SQL expert assistant that generates T-SQL SELECT queries for SQL Server. " +
                            "Use the database schema provided in the system message to generate accurate queries. " +
                            "Analyze the table structures, column names, and relationships in the schema context. " +
                            "Generate ONLY SELECT queries - no INSERT, UPDATE, DELETE, or DROP statements. " +
                            "Return ONLY the SQL query without any explanations, markdown formatting, or additional text. " +
                            "If the user request is unclear or doesn't match the schema, try to infer the intent and generate the most appropriate query. " +
                            "Make sure to use proper JOINs when querying related tables."+
                            "If the user greets you or makes casual conversation (e.g., \"hi,\" \"hello,\" \"how are you\"), respond politely and naturally."
                    });
            });

            services.AddSingleton(sp =>
            {
                var chatClient = sp.GetRequiredService<IChatClient>();
                return new ChatClientAgent(
                    chatClient,
                    new ChatClientAgentOptions
                    {
                        Name = "ChartAgent",
                        Instructions =
                            @"You will receive a table schema and sample rows in JSON.
Respond with a STRICT JSON object for Chart.js with fields:
-type: one of bar | line | pie | doughnut | scatter
- xKey: column name for X axis (prefer date / time or category)
-yKeys: array of 1 - 3 numeric column names
- title: short chart title
- Do not use markdown formatting or triple backticks.
Return ONLY the JSON(no prose).Choose sensible defaults."
                    });
            });
            services.AddSingleton(sp => {
                var chatClient = sp.GetRequiredService<IChatClient>();
                return new ChatClientAgent(
                    chatClient,
                    new ChatClientAgentOptions
                    {
                        Name = "TitleAgent",
                        Instructions =
                            @"You create concise chat titles (6-40 chars). No quotes. No trailing punctuation."
                    });
            });

            // 2) PDF agent (answers only from provided PDF context)
            services.AddSingleton(sp => {
                var chatClient = sp.GetRequiredService<IChatClient>();
                return new ChatClientAgent(
                    chatClient,
                    new ChatClientAgentOptions
                    {
                        Name = "PdfAgent",
                        Instructions =
                            "You answer questions using ONLY the provided PDF context. " +
                            "If the context is insufficient, say you don't know. " +
                            "Be concise and factual; include page hints when helpful."
                    });
            });

            
            services.AddSignalR();
            services.AddSingleton<IBackgroundJobQueue, BackgroundJobQueue>();
            services.AddSingleton<IJobStore, InMemoryJobStore>();
            services.AddHostedService<QueuedWorker>();
            //services.AddScoped<IVectorStoreFactory, VectorStoreFactory>();
            services.AddSingleton<IVectorStoreFactory, VectorStoreFactory>();
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
