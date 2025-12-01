using Microsoft.Extensions.DependencyInjection;
using ProductAnalysisApp.Services;
using ProductAnalysisAppWithMongoDb.Extensions;
using ProductAnalysisAppWithMongoDb.Utilities.AutoMapper;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("https://localhost:7109");

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});
builder.Services.AddControllers()
    .AddApplicationPart(typeof(ProductAnalysisApp.Presentation.AssemblyReferences).Assembly);
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient<PythonScraperService>();
// MongoDB, Repository & Service manager
builder.Services.ConfigureMongoContext(builder.Configuration);
builder.Services.ConfigureRepositoryManager();
builder.Services.ConfigureServiceManager();

// AutoMapper
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

// Firebase
//builder.Services.ConfigureFirebaseApp(builder.Configuration);
//builder.Services.ConfigureFirebaseAuthentication(builder.Configuration);
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

app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
