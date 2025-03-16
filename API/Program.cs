using API.Workers;
using Persistence.FileStores;
using Persistence.Interfaces;
using Slack;
using API.Services;
using API.MIddlewares;
using API.Interfaces.Invocations;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient<SlackClient>((client) =>
{
    client.BaseAddress = new Uri("https://www.slack.com/api/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<IOAuthStateStore, FileOAuthStateStore>();
builder.Services.AddScoped<IInstallationStore, FileInstallationStore>();
builder.Services.AddScoped<ISettingStore, FileSettingStore>();
builder.Services.AddScoped<IQueueStateStore, FileQueueStateStore>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PullRequestInvocation>();
builder.Services.AddScoped<IInvocation, PullRequestInvocation>(sp => sp.GetRequiredService<PullRequestInvocation>());
builder.Services.AddScoped<IInvocation, HomeTabInvocation>(sp =>
    new HomeTabInvocation(sp.GetRequiredService<ISettingStore>(), 
                          sp.GetRequiredService<QueueStateManager>(),
                          sp.GetRequiredService<PullRequestInvocation>())
);
builder.Services.AddTransient<SlackManager>();
builder.Services.AddTransient<QueueStateManager>();
builder.Services.AddTransient<SlackTokenRotator>();
builder.Services.AddHostedService<ReviewReminderWorker>();
builder.Services.AddHttpClient(ReviewReminderWorker.HttpClientName, (client) =>
{
    client.BaseAddress = new Uri("https://www.slack.com/api/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.UseWhen(
    context => context.Request.Path.ToString() == "/api/slack/commands"
                || context.Request.Path.ToString() == "/api/slack/interactions"
                || context.Request.Path.ToString() == "/api/slack/events",
    app =>
    {
        app.UseMiddleware<SlackSignatureVerifier>();
        app.UseMiddleware<InstallationTokenVerifier>();
    });

app.Run();