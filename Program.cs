#pragma warning disable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Agents;
using Microsoft.Extensions.Hosting;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using OpenAI.Chat;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;
using System.Text;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using System.Threading.Tasks;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

[Description("Greets user with name")]
string SayHello([Description("The user's name")] string name)
{
    return $"Hello, {name}! How can I assist you today?";
}

[Description("Plans a trip to the provided destination")]
async Task<string> PlanTrip([Description("The destination for the trip")] string destination, Kernel k)
{
    var cc = k.Services.GetKeyedService<ChatCompletionAgent>("travelagent");
    var sb = new StringBuilder();
    await foreach (var response in cc.InvokeAsync(new ChatMessageContent(AuthorRole.User, content: $"Plan a trip to {destination}"), new ChatHistoryAgentThread()))
    {
        sb.AppendLine(response.Message.Content);
    }
    return sb.ToString();
}

var kernel = builder.Services.AddKernel();

kernel.Services
    .AddTransient<IChatClient>((sp) =>
    {
        return new ChatClientBuilder(
            new AzureOpenAIClient(
                new Uri(Environment.GetEnvironmentVariable("AOAI_ENDPOINT")),
                new DefaultAzureCredential())
                .GetChatClient("gpt-4o-mini")
                .AsIChatClient())
            .Build()
            .AsKernelFunctionInvokingChatClient();
    });

builder.Services.AddKeyedTransient<ChatCompletionAgent>("userliaison",(sp,_) => {
    var kernel = sp.GetRequiredService<Kernel>().Clone();
    kernel.Plugins.AddFromFunctions("SayHello", [
        AIFunctionFactory.Create(SayHello).AsKernelFunction(),
        AIFunctionFactory.Create(PlanTrip).AsKernelFunction()]);
    var agent = new ChatCompletionAgent
    {
        Name = "UserLiaisonAgent",
        Description = "A user liaison agent that helps users with travel plans and expense reports.",
        Instructions = "Help users come up with a travel itinerary",
        Kernel = kernel.Clone(),
        Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }),
    };
    return agent;
});

kernel.Services.AddKeyedTransient<ChatCompletionAgent>("travelagent", (sp,_) => {
    return new ChatCompletionAgent
    {
        Name = "TravelAgent",
        Description = "A travel agent that helps users with travel plans and expense reports.",
        Instructions = "Help users come up with a travel itinerary",
        Kernel = sp.GetRequiredService<Kernel>().Clone(),
        Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }),
    };
});

var app = builder.Build();

var agent = app.Services.GetKeyedService<ChatCompletionAgent>("userliaison");
var thread = new ChatHistoryAgentThread();
while(true)
{
    Console.Write("User: ");
    var userInput = Console.ReadLine();
    if(string.IsNullOrEmpty(userInput))
    {
        break;
    }
    if(userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }
    Console.WriteLine("Bot: ");
    await foreach(var response in agent.InvokeAsync(new ChatMessageContent(AuthorRole.User, content: userInput),thread))
    {
        Console.Write($"{response.Message.Content}");
    }
    Console.WriteLine();
}