using DockAi.Viewer.Rendering;

var builder = WebApplication.CreateBuilder(args);

var apiBaseUrl = builder.Configuration.GetValue<string>("ApiBaseUrl") ?? "http://localhost:5108";

builder.Services.AddSingleton<RendererFactory>();
builder.Services.AddHttpClient("DockAiApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseCors();
app.UseStaticFiles();

// ── Rendering endpoints ──

// GET /render/{id} — Render a document to viewable HTML
app.MapGet("/render/{id}", async (string id, RendererFactory renderers, IHttpClientFactory httpFactory) =>
{
    var client = httpFactory.CreateClient("DockAiApi");
    var response = await client.GetAsync($"/api/document/{id}");
    if (!response.IsSuccessStatusCode)
        return Results.NotFound(new { error = "Document not found in API" });

    var doc = await response.Content.ReadFromJsonAsync<DocMetadata>();
    if (doc?.FilePath is null)
        return Results.BadRequest(new { error = "No file path in document metadata" });

    if (!File.Exists(doc.FilePath))
        return Results.NotFound(new { error = $"File not found: {doc.FileName}" });

    var renderer = renderers.GetRenderer(doc.FilePath);
    if (renderer is null)
        return Results.BadRequest(new { error = $"Unsupported file type: {Path.GetExtension(doc.FilePath)}" });

    try
    {
        var result = renderer.Render(doc.FilePath);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Rendering failed: {ex.Message}");
    }
});

// GET /raw/{id} — Serve the raw file (for PDF embed, downloads)
app.MapGet("/raw/{id}", async (string id, IHttpClientFactory httpFactory) =>
{
    var client = httpFactory.CreateClient("DockAiApi");
    var response = await client.GetAsync($"/api/document/{id}");
    if (!response.IsSuccessStatusCode)
        return Results.NotFound();

    var doc = await response.Content.ReadFromJsonAsync<DocMetadata>();
    if (doc?.FilePath is null || !File.Exists(doc.FilePath))
        return Results.NotFound();

    var contentType = GetContentType(doc.FileType);
    return Results.File(doc.FilePath, contentType, doc.FileName);
});

// ── Proxy endpoints (browser calls Viewer, Viewer forwards to Api) ──

app.MapGet("/proxy/search", async (string? q, int? max, IHttpClientFactory httpFactory) =>
{
    var client = httpFactory.CreateClient("DockAiApi");
    var url = $"/api/search?q={Uri.EscapeDataString(q ?? "")}&max={max ?? 50}";
    var stream = await client.GetStreamAsync(url);
    return Results.Stream(stream, "application/json");
});

app.MapGet("/proxy/document/{id}", async (string id, IHttpClientFactory httpFactory) =>
{
    var client = httpFactory.CreateClient("DockAiApi");
    var stream = await client.GetStreamAsync($"/api/document/{id}");
    return Results.Stream(stream, "application/json");
});

app.MapGet("/proxy/ontology", async (IHttpClientFactory httpFactory) =>
{
    var client = httpFactory.CreateClient("DockAiApi");
    var stream = await client.GetStreamAsync("/api/ontology");
    return Results.Stream(stream, "application/json");
});

app.MapGet("/proxy/taxonomy/{name}", async (string name, IHttpClientFactory httpFactory) =>
{
    var client = httpFactory.CreateClient("DockAiApi");
    var stream = await client.GetStreamAsync($"/api/taxonomy/{name}");
    return Results.Stream(stream, "application/json");
});

app.MapGet("/proxy/suggest", async (string? q, IHttpClientFactory httpFactory) =>
{
    var client = httpFactory.CreateClient("DockAiApi");
    var stream = await client.GetStreamAsync($"/api/suggest?q={Uri.EscapeDataString(q ?? "")}");
    return Results.Stream(stream, "application/json");
});

app.MapGet("/proxy/links/{id}", async (string id, IHttpClientFactory httpFactory) =>
{
    var client = httpFactory.CreateClient("DockAiApi");
    var stream = await client.GetStreamAsync($"/api/links/{id}");
    return Results.Stream(stream, "application/json");
});

app.MapPost("/proxy/index", async (IHttpClientFactory httpFactory) =>
{
    var client = httpFactory.CreateClient("DockAiApi");
    var response = await client.PostAsync("/api/index", null);
    var stream = await response.Content.ReadAsStreamAsync();
    return Results.Stream(stream, "application/json");
});

// SPA fallback
app.MapFallbackToFile("index.html");

app.Run();

// ── Supporting types ──

static string GetContentType(string? fileType) => fileType switch
{
    "pdf" => "application/pdf",
    "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    "csv" => "text/csv",
    "md" => "text/markdown",
    "html" or "htm" => "text/html",
    "txt" => "text/plain",
    _ => "application/octet-stream"
};

record DocMetadata(string? Id, string? Name, string? FileName, string? FilePath, string? FileType);
