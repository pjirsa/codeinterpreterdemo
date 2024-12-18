#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

builder.Services.AddKeyedSingleton("sksample", new Dictionary<string, string>
{
    { "sksample", bool.TrueString }
});

builder.Services.AddSingleton((_) => 
    OpenAIClientProvider.ForAzureOpenAI(new DefaultAzureCredential(new DefaultAzureCredentialOptions{ TenantId = builder.Configuration["AZURE_TENANT_ID"]! }), 
    new Uri(builder.Configuration["AzureOpenAI:Endpoint"]!)));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/chatwithassistant", async (string message,
[FromKeyedServices("sksample")] Dictionary<string, string> AssistantSampleMetadata,
[FromServices] OpenAIClientProvider clientProvider) =>
{
    // Define the agent
    OpenAIAssistantAgent agent =
        await OpenAIAssistantAgent.CreateAsync(
            clientProvider: clientProvider,
            definition: new("chat") // do the DI stuff with this TODO
            {
                EnableCodeInterpreter = true,
                Metadata = AssistantSampleMetadata,
            },
            kernel: new Kernel());

    // Create a thread for the agent conversation.
    string threadId = await agent.CreateThreadAsync(new OpenAIThreadCreationOptions { Metadata = AssistantSampleMetadata });

    // Respond to user input
    try
    {
        //await InvokeAgentAsync("Use code to determine the values in the Fibonacci sequence that are less than the value of 101?");
        var result = await InvokeAgentAsync(message);
        return Results.Ok(result);
    }
    finally
    {
        await agent.DeleteThreadAsync(threadId);
        await agent.DeleteAsync();
    }

    // Local function to invoke agent and display the conversation messages.
    async Task<ChatMessageContent[]> InvokeAgentAsync(string input)
    {
        List<ChatMessageContent> results = [];
        ChatMessageContent message = new(AuthorRole.User, input);
        await agent.AddChatMessageAsync(threadId, message);

        await foreach (ChatMessageContent response in agent.InvokeAsync(threadId))
        {
            results.Add(response);
        }

        return [.. results];
    }


})
.WithName("ChatWithAssistant");

app.Run();
