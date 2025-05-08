#pragma warning disable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Agents;
using Microsoft.Extensions.Hosting;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using OpenAI.Chat;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

[Description("A tool to greet users")]
string SayHello([Description("The user name")] string username)
{
    return $"Hello, {username}! How can I assist you today?";
}

builder
    .Services
    .AddKernel()
    .Services
    .AddTransient<IChatClient>((sp) =>
    {
        return new ChatClientBuilder(
            new AzureOpenAIClient(
                new Uri(Environment.GetEnvironmentVariable("AOAI_ENDPOINT")),
                new DefaultAzureCredential())
                .GetChatClient("gpt-4o-mini")
                .AsIChatClient())
            .UseFunctionInvocation()
            .ConfigureOptions(_ =>
                _.Tools = [
                    AIFunctionFactory.Create(SayHello)
                ])
            .Build()
            .AsKernelFunctionInvokingChatClient();
    });

builder.Services.AddTransient<ChatCompletionAgent>(sp =>
{
    return new ChatCompletionAgent
    {
        Name = "UserLiaison",
        Description = "A travel agent that helps users with travel plans and expense reports.",
        Instructions =
            """
            ## User liaison agent
            You are a travel agent, liaising with a user, '{{userName}}'.

            ## Greeting the user
            Use the 'SayHello' tool to greet the user.
            """,
        Kernel = sp.GetRequiredService<Kernel>()
    };
}); 

var app = builder.Build();

var agent = app.Services.GetService<ChatCompletionAgent>();

while(true)
{
    Console.Write("You: ");
    string? userInput = Console.ReadLine();

    if (string.Equals(userInput, "exit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Goodbye!");
        break;
    }

    ChatMessageContent message = new(AuthorRole.User, userInput);

    await foreach(AgentResponseItem<ChatMessageContent> response in agent.InvokeAsync(message))
    {
        Console.WriteLine(response.Message);
    }
}