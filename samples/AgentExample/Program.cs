using Microsoft.Extensions.AI;
using OpenAI;
using System.Text.Json.Serialization;

namespace AgentExample;

public class Program
{
    public class WeatherTool : AITool, ITool
    {
        public new string Name => "weather";
        public new string Description => "Provides weather information for a given location.";

        public async Task<object> Run(Dictionary<string, object> input)
        {
            await Task.Delay(100);
            return "It's a sunny day";
        }
    }

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        // Shared MCP transport
        /*
        var mcpClient = await McpClientFactory.CreateAsync(
            new()
            {
                Id = "everything",
                Name = "Everything",
                TransportType = TransportTypes.StdIo,
                TransportOptions = new()
                {
                    ["command"] = "npx", ["arguments"] = "-y @modelcontextprotocol/server-everything",
                }
            });
        */
            /*
            EnvironmentVariables = new Dictionary<string, string>
            {
                { "OPENAI_API_KEY", "your-openai-key" }
            }
            */
        const string modelId = "gpt-4o-mini";
        
        using IChatClient chatClient =
            new OpenAIClient(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
            .AsChatClient(modelId)
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();

        var chatOptions = new ChatOptions
        {
            ModelId = modelId,
            MaxOutputTokens = 1000,
            Temperature = 0.7f,
            Tools = new List<AITool> { new WeatherTool() },
            //ConversationToolChoice = ConversationToolChoice.Auto,
            //Tools = await mcpClient.ListToolsAsync()

        };
        var weatherTool = new WeatherTool();
        var tools = new List<ITool> { weatherTool };
        var agent = new Agent(modelId, "You are a helpful assistant.", tools, chatClient);

        _ = app.MapGet("/agent", async (string message) =>
        {
            // "What's the weather like today?"
            var response = await agent.Chat(message, chatOptions);

            return Results.Json(
                new WeatherResponse(response),
                    AppJsonSerializerContext.Default.WeatherResponse
                );
        });

        await app.RunAsync();
    }
}


public record class WeatherResponse(ChatResponse response)
{
    [JsonPropertyName("response")]
    public ChatResponse Response { get; init; } = response;
}

[JsonSerializable(typeof(WeatherResponse))]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}

public interface ITool
{
    string Name { get; }
    string Description { get; }
    Task<object> Run(Dictionary<string, object> input);
}