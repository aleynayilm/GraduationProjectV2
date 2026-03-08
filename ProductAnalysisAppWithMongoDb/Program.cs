using Microsoft.Extensions.DependencyInjection;
using ProductAnalysisApp.Services;
using ProductAnalysisApp.Services.Messaging;
using ProductAnalysisAppWithMongoDb.Extensions;
using ProductAnalysisAppWithMongoDb.Infrastructure;
using ProductAnalysisAppWithMongoDb.Middleware;
using ProductAnalysisAppWithMongoDb.Utilities.AutoMapper;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("https://localhost:7109");
FirebaseInitializer.Initialize(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});
builder.Services.AddControllers()
    .AddApplicationPart(typeof(ProductAnalysisApp.Presentation.AssemblyReferences).Assembly);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
//RabbitMQ
builder.Services.AddSingleton(sp =>
    RabbitMqPublisher.CreateAsync(
        builder.Configuration["RabbitMQ:Host"] ?? "localhost"
    ).GetAwaiter().GetResult()
);
// Redis
var redisConn = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConn));
builder.Services.AddScoped<RedisService>();

// FCM
builder.Services.AddScoped<FcmService>();
builder.Services.AddHostedService<JobResultConsumer>();
builder.Services.AddHttpClient<PythonScraperService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.ConfigureMongoContext(builder.Configuration);
builder.Services.ConfigureRepositoryManager();
builder.Services.ConfigureServiceManager();

builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());
builder.Services.AddAuthentication("Firebase")
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
               FirebaseAuthHandler>("Firebase", _ => { });
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(
       c =>
       {
           c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
           c.RoutePrefix = "swagger";
       });
}
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowAll");
app.UseMiddleware<FirebaseAuthMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
