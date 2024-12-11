using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithImage("ankane/pgvector")
    .WithImageTag("latest")
    .WithLifetime(ContainerLifetime.Persistent);

var redis = builder.AddRedis("redis");

var catalogDb = postgres.AddDatabase("catalogdb");

var identityDb = postgres.AddDatabase("IdentityDB");

var launchProfileName = ShouldUseHttpForEndpoints() ? "http" : "https";

var identityApi = builder.AddProject<Projects.Idintity_API>("identity-api", launchProfileName)
    .WithExternalHttpEndpoints()
    .WithReference(identityDb);

var identityEndpoint = identityApi.GetEndpoint(launchProfileName);

var basketApi = builder.AddProject<Projects.Basket_API>("basket-api")
    .WithReference(redis)
    .WithEnvironment("Identity__Url", identityEndpoint);

var catalogApi = builder.AddProject<Projects.Catalog_API>("catalog-api")
    .WithReference(catalogDb);

var webApp = builder.AddProject<Projects.WebApp>("webapp", launchProfileName)
    .WithExternalHttpEndpoints()
    .WithReference(basketApi)
    .WithReference(catalogApi)
    .WithEnvironment("IdentityUrl", identityEndpoint);

const string openAIName = "openai";
const string chatModelName = "gpt-4o";

IResourceBuilder<IResourceWithConnectionString> openAI;
openAI = builder.AddConnectionString(openAIName);

var AzureOpenAI_key = builder.Configuration["ConnectionStrings:openai:Key"];
var AzureOpenAI_endpoint = builder.Configuration["ConnectionStrings:openai:Endpoint"];

webApp
    .WithReference(openAI)
    .WithEnvironment("AI__OPENAI__CHATMODEL", chatModelName)
    .WithEnvironment("ConnectionStrings:openai:Endpoint", AzureOpenAI_endpoint)
    .WithEnvironment("ConnectionStrings:openai:Key", AzureOpenAI_key);


webApp.WithEnvironment("CallBackUrl", webApp.GetEndpoint(launchProfileName));

// Identity has a reference to all of the apps for callback urls, this is a cyclic reference
identityApi.WithEnvironment("WebAppClient", webApp.GetEndpoint(launchProfileName));

builder.Build().Run();
static bool ShouldUseHttpForEndpoints()
{
    const string EnvVarName = "ESHOP_USE_HTTP_ENDPOINTS";
    var envValue = Environment.GetEnvironmentVariable(EnvVarName);

    // Attempt to parse the environment variable value; return true if it's exactly "1".
    return int.TryParse(envValue, out int result) && result == 1;
}
