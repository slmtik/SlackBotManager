using SlackBotManager.API.Interfaces;
using SlackBotManager.API.MIddlewares;
using SlackBotManager.API.Services;

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
builder.Services.AddScoped<IInstallationRepository, FileInstallationRepository>();
builder.Services.AddScoped<ISettingRepository, FileSettingRepository>();
builder.Services.AddTransient<AuthorizationUrlGenerator>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CreatePullRequestInvocation>();
builder.Services.AddScoped<HomeTabInvocation>();
builder.Services.AddTransient<SlackMessageManager>();

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
        app.UseMiddleware<SlackTokenRotator>();
    });

app.Run();