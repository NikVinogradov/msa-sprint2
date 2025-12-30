using Booking;
using BookingServiceApp.Grpc;
using BookingServiceApp.Repositories;
using BookingServiceApp.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(9090, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

builder.Services.AddGrpc();

builder.Services.AddSingleton<BookingRepository>();
builder.Services.AddSingleton<BookingDomainService>();
builder.Services.AddSingleton<BookingEventProducer>();
builder.Services.AddHostedService<DbInitializer>();

builder.Services.AddHttpClient<MonolithClient>()
    .ConfigureHttpClient((sp, client) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var baseUrl = config["Monolith:BaseUrl"] ?? "http://monolith:8080";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(3);
    });

var app = builder.Build();

app.MapGrpcService<GrpcBookingService>();
app.MapGet("/", () => "booking-service gRPC");

app.Run();
