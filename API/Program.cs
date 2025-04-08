using API.Workers;
using Persistence.FileStores;
using Persistence.Interfaces;
using Slack;
using API.Services;
using API.MIddlewares;
using API.Interfaces.Invocations;
using API.Interfaces;
using API.VersionStrategists;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient<SlackClient>((client) =>
{
    client.BaseAddress = new Uri("https://slack.com/api/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<IOAuthStateStore, FileOAuthStateStore>();
builder.Services.AddScoped<IInstallationStore, FileInstallationStore>();
builder.Services.AddScoped<ISettingStore, FileSettingStore>();
builder.Services.AddScoped<IQueueStateStore, FileQueueStateStore>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PullRequestInvocation>();
builder.Services.AddScoped<IInvocation, PullRequestInvocation>(sp => sp.GetRequiredService<PullRequestInvocation>());
builder.Services.AddScoped<IInvocation, HomeTabInvocation>();
builder.Services.AddScoped<SlackManager>();
builder.Services.AddScoped<SlackOAuthHelper>();
builder.Services.AddScoped<QueueStateManager>();
builder.Services.AddScoped<SlackTokenRotator>();
builder.Services.AddHostedService<ReviewReminderWorker>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IVersionStrategistResolver, VersionStrategistResolver>();
builder.Services.AddScoped<IVersionStrategist, NoVersionStrategist>();
builder.Services.AddScoped<IVersionStrategist, ManualVersionStrategist>();
builder.Services.AddScoped<IVersionStrategist, ParseLoginPageVersionStrategist>();
builder.Services.AddScoped<WebhookSender>();
builder.Services.AddScoped<ChannelMonitorVersionStrategist>();
builder.Services.AddScoped<IVersionStrategist>(sp => sp.GetRequiredService<ChannelMonitorVersionStrategist>());
builder.Services.AddScoped<IInvocation>(sp => sp.GetRequiredService<ChannelMonitorVersionStrategist>());

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    builder.Logging.AddEventLog(new Microsoft.Extensions.Logging.EventLog.EventLogSettings()
    {
        SourceName = "SlackBotManager"
    });
    builder.Host.UseWindowsService();
}

var app = builder.Build();

bool useHttps = builder.Configuration.GetValue("UseHttpsRedirection", true);
if (useHttps)
{
    app.UseHttpsRedirection();
}

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