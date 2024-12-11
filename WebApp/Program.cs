
using WebApp.Components;
using eShop.WebApp.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.IdentityModel.JsonWebTokens;
using Azure.AI.OpenAI;
using Azure;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
.AddInteractiveServerComponents();

// Register the chat client for Azure OpenAI
builder.Services.AddSingleton<IChatClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();

    // Read the OpenAI connection string
    var openAIConnectionString = configuration.GetConnectionString("openai");
    if (string.IsNullOrWhiteSpace(openAIConnectionString))
        throw new InvalidOperationException("Connection string 'openai' is missing.");

    // Parse the connection string
    var endpointValue = openAIConnectionString.Split(';').FirstOrDefault(s => s.StartsWith("Endpoint="))?.Replace("Endpoint=", "");
    var apiKeyValue = openAIConnectionString.Split(';').FirstOrDefault(s => s.StartsWith("Key="))?.Replace("Key=", "");

    // Read environment variable for OpenAI Chat Model
    var deploymentName = configuration["AI:OpenAI:ChatModel"];
    if (string.IsNullOrWhiteSpace(deploymentName))
        throw new InvalidOperationException("Configuration key 'AI:OpenAI:ChatModel' is missing.");

    // Validate connection string values
    if (string.IsNullOrWhiteSpace(endpointValue))
        throw new InvalidOperationException("The 'Endpoint' value in the OpenAI connection string is missing.");
    if (string.IsNullOrWhiteSpace(apiKeyValue))
        throw new InvalidOperationException("The 'Key' value in the OpenAI connection string is missing.");

    Console.WriteLine($"AI Chat Model: {deploymentName}");
    Console.WriteLine($"OpenAI Endpoint: {endpointValue}");
    Console.WriteLine($"OpenAI API Key: {apiKeyValue}");

    // Create AzureKeyCredential and OpenAI client
    if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint))
        throw new InvalidOperationException($"Azure OpenAI Endpoint '{endpointValue}' is not a valid URI.");

    var credentials = new AzureKeyCredential(apiKeyValue);
    var client = new AzureOpenAIClient(endpoint, credentials).AsChatClient(deploymentName);

    return new ChatClientBuilder(client)
        .UseFunctionInvocation()
        .Build();
});


builder.AddApplicationServices();

//builder.AddAuthenticationServices();

//builder.Services.AddHttpForwarderWithServiceDiscovery();

//builder.Services.AddSingleton<IProductImageUrlProvider, ProductImageUrlProvider>();
//builder.Services.AddHttpClient<CatalogService>(o => o.BaseAddress = new("http://localhost:5301"))
//    .AddApiVersion(1.0);

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapForwarder("/product-images/{id}", "http://localhost:5301", "/api/catalog/items/{id}/pic");

app.Run();