using MyTeamsAppSK_BotSSO;
using MyTeamsAppSK_BotSSO.Commands;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.TeamsFx.Conversation;
using Microsoft.Bot.Builder;
using Microsoft.TeamsFx.Configuration;
using MyTeamsAppSK_BotSSO.SSO;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient("WebClient", client => client.Timeout = TimeSpan.FromSeconds(600));
builder.Services.AddHttpContextAccessor();

// Prepare Configuration for ConfigurationBotFrameworkAuthentication
var config = builder.Configuration.Get<ConfigOptions>();
builder.Configuration["MicrosoftAppType"] = "MultiTenant";
builder.Configuration["MicrosoftAppId"] = config.BOT_ID;
builder.Configuration["MicrosoftAppPassword"] = config.BOT_PASSWORD;

// Create the Bot Framework Authentication to be used with the Bot Adapter.
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

builder.Services.AddRazorPages();

// Create the Bot Framework Adapter with error handling enabled.                                        
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

builder.Services.AddSingleton<IStorage, MemoryStorage>();
// Create the Conversation state. (Used by the Dialog system itself.)
builder.Services.AddSingleton<ConversationState>();

// The Dialog that will be run by the bot.
builder.Services.AddSingleton<SsoDialog>();

// Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
builder.Services.AddTransient<IBot, TeamsSsoBot<SsoDialog>>();

builder.Services.AddOptions<BotAuthenticationOptions>().Configure(options =>
{
    options.ClientId = config.TeamsFx.Authentication.ClientId;
    options.ClientSecret = config.TeamsFx.Authentication.ClientSecret;
    options.OAuthAuthority = config.TeamsFx.Authentication.OAuthAuthority;
    options.ApplicationIdUri = config.TeamsFx.Authentication.ApplicationIdUri;
    options.InitiateLoginEndpoint = config.TeamsFx.Authentication.Bot.InitiateLoginEndpoint;
});

// Create the Cloud Adapter with error handling enabled.
// Note: some classes expect a BotAdapter and some expect a BotFrameworkHttpAdapter, so
// register the same adapter instance for both types.
builder.Services.AddSingleton<CloudAdapter, AdapterWithErrorHandler>();
builder.Services.AddSingleton<IBotFrameworkHttpAdapter>(sp => sp.GetService<CloudAdapter>());
builder.Services.AddSingleton<BotAdapter>(sp => sp.GetService<CloudAdapter>());

builder.Services.AddSingleton(sp =>
{
    var options = new ConversationOptions()
    {
        Adapter = sp.GetService<CloudAdapter>(),
        Command = new CommandOptions()
        {
            Commands = new List<ITeamsCommandHandler> { }
        }
    };

    return new ConversationBot(options);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseStaticFiles();
app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapRazorPages();
});

app.Run();