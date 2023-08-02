using Microsoft.TeamsFx.Configuration;

namespace MyTeamsAppSK_BotSSO
{
    public class TeamsFxOptions
    {
        public AuthenticationOptions Authentication { get; set; }
    }

    public class ConfigOptions
    {
        public string BOT_ID { get; set; }
        public string BOT_PASSWORD { get; set; }
        public TeamsFxOptions TeamsFx { get; set; }
    }
}
