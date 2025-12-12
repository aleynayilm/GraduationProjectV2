using Microsoft.Extensions.DependencyInjection;
using ProductAnalysisApp.Services;
using ProductAnalysisAppWithMongoDb.Extensions;
using ProductAnalysisAppWithMongoDb.Utilities.AutoMapper;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("https://localhost:7109");

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
builder.Services.AddHttpClient<PythonScraperService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.ConfigureMongoContext(builder.Configuration);
builder.Services.ConfigureRepositoryManager();
builder.Services.ConfigureServiceManager();

builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());
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
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
