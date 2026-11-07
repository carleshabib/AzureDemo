using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

var builder = WebApplication.CreateBuilder(args);

Azure.Core.TokenCredential credential = builder.Environment.IsDevelopment()
    ? new AzureCliCredential()
    : new DefaultAzureCredential();


var kvUri = builder.Configuration["KeyVaultUri"];
if (!string.IsNullOrEmpty(kvUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(kvUri), credential);
}



// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton(_ =>
{
var url = builder.Configuration["StorageAccountUrl"]
    ?? throw new InvalidOperationException("StorageAccountUrl is missing in configuration");    
var service = new BlobServiceClient(new Uri(url), credential);
    return service.GetBlobContainerClient("photos");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapGet("/health", () => Results.Ok(new {
    status = "ok",
    version = "v2",
    env = app.Environment.EnvironmentName,
    time = DateTime.UtcNow
}));

app.MapGet("/secret", (IConfiguration config) =>
{
    var value = config["DemoSecret"];
    return Results.Ok(new {
        found = value is not null,
        preview = value is null ? null : value[..5] + "***"
    });
});

// Uploading photos
app.MapPost("/photos", async (IFormFile file, BlobContainerClient container) =>
{
    var name = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
    var blob = container.GetBlobClient(name);
    await using var stream = file.OpenReadStream();
    await blob.UploadAsync(stream, new BlobUploadOptions
    {
        HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType }
    });
    return Results.Created($"/photos/{name}", new { name });
}).DisableAntiforgery();

// photo listing
app.MapGet("/photos", async (BlobContainerClient container) =>
{
    var list = new List<object>();
    await foreach (var b in container.GetBlobsAsync())
        list.Add(new { name = b.Name, size = b.Properties.ContentLength, uploaded = b.Properties.CreatedOn });
    return Results.Ok(list);
});

// عرض صورة
app.MapGet("/photos/{name}", async (string name, BlobContainerClient container) =>
{
    var blob = container.GetBlobClient(name);
    if (!await blob.ExistsAsync()) return Results.NotFound();
    var download = await blob.DownloadStreamingAsync();
    return Results.Stream(download.Value.Content, download.Value.Details.ContentType);
});

// Deleting photos
app.MapDelete("/photos/{name}", async (string name, BlobContainerClient container) =>
{
    var deleted = await container.DeleteBlobIfExistsAsync(name);
    return deleted.Value ? Results.NoContent() : Results.NotFound();
});


app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
