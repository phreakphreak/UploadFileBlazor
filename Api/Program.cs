using System.Globalization;
using Microsoft.Extensions.FileProviders;

var MyConfigCors = "CorsPolicy";
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyConfigCors, policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
}
app.UseStaticFiles();

app.UseHsts();
app.UseHttpsRedirection();

app.UseCors(MyConfigCors);


app.UseAuthorization();
app.MapGet("/", () => "Hello World!");

app.MapControllers();


app.Run();