using Azure.Core;
using Microsoft.Bot.Builder;
using Microsoft.Graph;
using Microsoft.TeamsFx.Configuration;
using MyTeamsAppSK_BotSSO.AI;

namespace MyTeamsAppSK_BotSSO.SSO;

public static class SsoOperations
{
    public static async Task ShowUserInfo(ITurnContext stepContext, string token, BotAuthenticationOptions botAuthOptions)
    {
        await stepContext.SendActivityAsync("Calling Microsoft Graph by Semantic Kernel ...");
        var authProvider = new DelegateAuthenticationProvider((requestMessage) =>
        {
            requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return Task.CompletedTask;
        });
        var graphClient = new GraphServiceClient(authProvider);
        var result = await SemanticKernel.ProcessRequest(graphClient);
        await stepContext.SendActivityAsync(result);
    }
}