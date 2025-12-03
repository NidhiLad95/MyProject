using GenxAi_Solutions.Models;
using GenxAi_Solutions.Services;
using GenxAi_Solutions.Services.Hubs;
using GenxAi_Solutions.Services.Interfaces;
using GenxAi_Solutions.Utils;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SQLitePCL;
using System.Text;


var builder = WebApplication.CreateBuilder(args);
Batteries.Init();

// Clear all default logging providers
builder.Logging.ClearProviders();

// Configure logging
var logPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");

// Add information file logger
builder.Logging.AddFileLogger(logPath, "Information");

// Add error file logger
builder.Logging.AddFileLogger(logPath, "Error");

// Add audit file logger
builder.Logging.AddFileLogger(logPath, "Audit");

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddApplicationServices(builder.Configuration);

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
// builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();



//// ? Add cookie authentication
//builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
//    .AddCookie(options =>
//    {
//        options.LoginPath = "/User/Login";
//        options.LogoutPath = "/User/Logout";
//    });

// Create a wrapper service for auth events
builder.Services.AddSingleton<AuthEventsService>();

//// Add cookie authentication with fixed event handlers
//builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
//    .AddCookie(options =>
//    {
//        options.LoginPath = "/User/Login";
//        options.LogoutPath = "/User/Logout";

//        // Fixed: Use service locator pattern to resolve scoped services
//        options.Events = new CookieAuthenticationEvents
//        {
//            OnSignedIn = context =>
//            {
//                var authEventsService = context.HttpContext.RequestServices.GetRequiredService<AuthEventsService>();
//                return authEventsService.OnSignedIn(context);
//            },
//            OnSigningOut = context =>
//            {
//                var authEventsService = context.HttpContext.RequestServices.GetRequiredService<AuthEventsService>();
//                return authEventsService.OnSigningOut(context);
//            }
//        };
//    });

builder.Services
    .AddAuthentication(
    //CookieAuthenticationDefaults.AuthenticationScheme
    //JwtBearerDefaults.AuthenticationScheme
    options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    }
    )
    //.AddCookie(options =>
    //{
    //    options.LoginPath = "/User/Login";
    //    options.LogoutPath = "/User/Logout";
    //    options.Events = new CookieAuthenticationEvents
    //    {
    //        OnSignedIn = context =>
    //        {
    //            var svc = context.HttpContext.RequestServices.GetRequiredService<AuthEventsService>();
    //            return svc.OnSignedIn(context);
    //        },
    //        OnSigningOut = context =>
    //        {
    //            var svc = context.HttpContext.RequestServices.GetRequiredService<AuthEventsService>();
    //            return svc.OnSigningOut(context);
    //        }
    //    };
    //})
    // NEW: Add JwtBearer (Cookie remains the default)
    .AddJwtBearer( options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt");
        //options.SaveToken = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // Allow SignalR tokens via query string for your hubs
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/hubs/semantic") || path.StartsWithSegments("/hubs/notifications")))
                {
                    context.Token = accessToken;
                }
                // return Task.CompletedTask;

                var svc = context.HttpContext.RequestServices.GetRequiredService<AuthEventsService>();
                return svc.OnMessageReceived(context);
            },
            // Cookie OnSignedIn
            OnTokenValidated = context =>
            {
                var svc = context.HttpContext.RequestServices.GetRequiredService<AuthEventsService>();
                return svc.OnTokenValidated(context);
            },

            OnAuthenticationFailed = context =>
            {
                var svc = context.HttpContext.RequestServices.GetRequiredService<AuthEventsService>();
                return svc.OnAuthenticationFailed(context);
            },

            // Customize 401 JSON & audit (useful replacement for redirects in APIs)
            OnChallenge = context =>
            {
                var svc = context.HttpContext.RequestServices.GetRequiredService<AuthEventsService>();
                return svc.OnChallenge(context);
            },

            // Customize 403 JSON & audit
            OnForbidden = context =>
            {
                var svc = context.HttpContext.RequestServices.GetRequiredService<AuthEventsService>();
                return svc.OnForbidden(context);
            },
        };
    });

builder.Services.AddDistributedMemoryCache();
//builder.Services.AddSession(options =>
//{
//    options.IdleTimeout = TimeSpan.FromMinutes(30); // session timeout
//    options.Cookie.HttpOnly = true;
//    options.Cookie.IsEssential = true;
//});

//builder.Services.Configure<HostOptions>(o =>
//{
//    // Don’t tear down the whole app if a BackgroundService throws once
//    o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
//});


var app = builder.Build();

// global middleware
app.UseGenxAiCorePipeline();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseDeveloperExceptionPage();
    //app.UseSwagger();
    //app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();


app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()); // <-- this allows DELETE, PUT, etc.

//app.UseSession();
app.UseAuthentication(); // ? enable auth

app.UseAuthorization();

// Map hub (route used by frontend)
app.MapHub<SemanticHub>("/semanticHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=User}/{action=Login}/{id?}");

app.MapHub<SemanticHub>("/hubs/semantic");   // for popup notifications
app.MapHub<NotificationHub>("/hubs/notifications"); // for notification bedge


app.Run();
