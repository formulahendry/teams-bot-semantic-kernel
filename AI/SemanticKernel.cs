using Microsoft.Graph;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Skills.MsGraph.Connectors.Client;
using Microsoft.SemanticKernel.Skills.MsGraph.Connectors.CredentialManagers;
using Microsoft.SemanticKernel.Skills.MsGraph.Connectors;
using Microsoft.SemanticKernel.Skills.MsGraph;
using System.Reflection;
using System.Net.Http.Headers;

namespace MyTeamsAppSK_BotSSO.AI
{
    public class SemanticKernel
    {
        static public async Task<string> ProcessRequest(GraphServiceClient graphServiceClient)
        {
            #region Initialization

            // Load configuration
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddJsonFile(path: "appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile(path: "appsettings.Development.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<Program>()
                .Build();

            // Initialize logger
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConfiguration(configuration.GetSection("Logging"))
                    .AddConsole()
                    .AddDebug();
            });

            ILogger<Program> logger = loggerFactory.CreateLogger<Program>();


            string? currentAssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrWhiteSpace(currentAssemblyDirectory))
            {
                throw new InvalidOperationException("Unable to determine current assembly directory.");
            }

            #endregion

            // Initialize SK Graph API Skills we'll be using in the plan.
            CloudDriveSkill oneDriveSkill = new(new OneDriveConnector(graphServiceClient), loggerFactory.CreateLogger<CloudDriveSkill>());
            TaskListSkill todoSkill = new(new MicrosoftToDoConnector(graphServiceClient), loggerFactory.CreateLogger<TaskListSkill>());
            EmailSkill outlookSkill = new(new OutlookMailConnector(graphServiceClient), loggerFactory.CreateLogger<EmailSkill>());

            // Initialize the Semantic Kernel and and register connections with OpenAI/Azure OpenAI instances.
            KernelBuilder builder = Kernel.Builder
                .WithLogger(loggerFactory.CreateLogger<IKernel>());

            var azureOpenAIConfiguration = configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
            builder.WithAzureTextCompletionService(
                deploymentName: azureOpenAIConfiguration.DeploymentName,
                endpoint: azureOpenAIConfiguration.Endpoint,
                apiKey: azureOpenAIConfiguration.ApiKey);

            IKernel sk = builder.Build();

            var onedrive = sk.ImportSkill(oneDriveSkill, "onedrive");
            var todo = sk.ImportSkill(todoSkill, "todo");
            var outlook = sk.ImportSkill(outlookSkill, "outlook");

            var pluginsDirectory = Path.Combine(currentAssemblyDirectory, "AI", "plugins");

            IDictionary<string, ISKFunction> summarizePlugin = sk.ImportSemanticSkillFromDirectory(pluginsDirectory, "SummarizePlugin");

            //
            // The static plan below is meant to emulate a plan generated from the following request:
            // "Summarize the content of cheese.txt and send me an email with the summary and a link to the file. Then add a reminder to follow-up next week."
            //
            string? pathToFile = configuration["OneDrivePathToFile"];
            if (string.IsNullOrWhiteSpace(pathToFile))
            {
                throw new InvalidOperationException("OneDrivePathToFile is not set in configuration.");
            }

            // Get file content
            SKContext fileContentResult = await sk.RunAsync(pathToFile,
               onedrive["GetFileContent"],
               summarizePlugin["Summarize"]);
            if (fileContentResult.ErrorOccurred)
            {
               throw new InvalidOperationException($"Failed to get file content: {fileContentResult.LastErrorDescription}");
            }

            string fileSummary = fileContentResult.Result;

            // string fileSummary = "Semantic Kernel (SK) is an SDK that enables developers to integrate AI Large Language Models (LLMs) with conventional programming languages. It provides a range of features such as prompt templating, function chaining, vectorized memory, and intelligent planning capabilities. SK is open-source, allowing developers to join the community and build AI-first apps faster.";

            // Get my email address
            SKContext emailAddressResult = await sk.RunAsync(string.Empty, outlook["GetMyEmailAddress"]);
            string myEmailAddress = emailAddressResult.Result;

            // Create a link to the file
            SKContext fileLinkResult = await sk.RunAsync(pathToFile, onedrive["CreateLink"]);
            string fileLink = fileLinkResult.Result;

            // Send me an email with the summary and a link to the file.
            ContextVariables emailMemory = new($"{fileSummary}{Environment.NewLine}{Environment.NewLine}{fileLink}");
            emailMemory.Set(EmailSkill.Parameters.Recipients, myEmailAddress);
            emailMemory.Set(EmailSkill.Parameters.Subject, $"[SK] Summary of {pathToFile}");

            await sk.RunAsync(emailMemory, outlook["SendEmail"]);

            // Add a reminder to follow-up next week.
            ContextVariables followUpTaskMemory = new($"Follow-up about {pathToFile}.");
            DateTimeOffset nextMonday = TaskListSkill.GetNextDayOfWeek(System.DayOfWeek.Monday, TimeSpan.FromHours(9));
            followUpTaskMemory.Set(TaskListSkill.Parameters.Reminder, nextMonday.ToString("o"));
            await sk.RunAsync(followUpTaskMemory, todo["AddTask"]);

            logger.LogInformation("Done!");

            return "Process is done!";
        }
    }
}
