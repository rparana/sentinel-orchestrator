var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .AddDatabase("sentineldb");

var redis = builder.AddRedis("redis");

var api = builder.AddDotnetProject("sentinel-api", "../Sentinel.Api/Sentinel.Api.csproj")
    .WithReference(postgres)
    .WithReference(redis)
    .WaitFor(postgres)
    .WaitFor(redis)
    .WithHttpHealthCheck("/health");

builder.AddJavaScriptApp("sentinel-web", "../sentinel-web")
    .WithPnpm()
    .WithRunScript("start")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();
