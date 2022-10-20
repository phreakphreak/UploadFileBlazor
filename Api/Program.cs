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
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "upload")),
    RequestPath = "/public"
});
// app.UseStaticFiles();

app.UseHsts();
// app.UseHttpsRedirection();

app.UseCors(MyConfigCors);

// app.Use(async (context, next) =>
// {
//     // if (context.Request.Form.Files.Count > 0)
//     // {
//     //     // var file = context.Request.Form.Files[0];
//     //     // Console.WriteLine(Path.GetFileName(file.FileName));
//     //     // using Stream stream = file.OpenReadStream();
//     //     // using StreamReader reader = new(stream);
//     //     // string data = await reader.ReadToEndAsync();
//     //     Console.WriteLine("files");

//     //     // Do something with file data
//     // }
//     Console.WriteLine(context.Request.ToString());

//     await next();
// });

app.UseAuthorization();
app.MapGet("/", () => "Hello World!");

app.MapControllers();


app.Run();