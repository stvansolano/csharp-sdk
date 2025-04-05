using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.Text.Json;

namespace AgentExample;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("MCP Agent Example");
        
        // Create a logger factory for better debugging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Debug);
        });

        Dictionary<string, string> transportOptions = new()
        {
            ["command"] = "npx",
            ["arguments"] = "-y @modelcontextprotocol/server-everything",
        };

        // Create an MCP server that runs an external time server process
        // This is similar to the Deno example in the PydanticAI code
        var timeServer = new McpServerStdio(
            "npx", // Command to run
            new[] { "-y", "@modelcontextprotocol/server-time" }, // Arguments
            loggerFactory: loggerFactory
        );
        
        // Create an agent using GPT-4o model (or any other model ID)
        // In a real implementation, this would connect to the actual LLM API
        var agent = new Agent("openai:gpt-4o", [timeServer]);
        
        try
        {
            // Run the MCP servers in a scope similar to the async with statement in Python
            // This starts the servers when entering the scope and stops them when exiting
            await using (var scope = agent.RunMcpServers(transportOptions))
            {
                Console.WriteLine("MCP servers started. Running example query...");
                
                // Run a prompt that's similar to the PydanticAI example
                var result = await agent.RunAsync("How many days between 2000-01-01 and 2025-03-18?");
                
                // Display the result
                Console.WriteLine("Result from LLM:");
                Console.WriteLine(result.Data);
                
                // Display tool calls that were made
                Console.WriteLine("\nTool calls made:");
                foreach (var toolCall in result.ToolCalls)
                {
                    Console.WriteLine($"- Tool: {toolCall.Name}");
                    Console.WriteLine($"  Result: {toolCall.ToString()}");
                }
            }
            
            Console.WriteLine("MCP servers have been stopped.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            await agent.DisposeAsync();
        }
    }
}

/*
/// <summary>
/// A mock implementation of IChatClient for demonstration purposes.
/// In a real application, this would be replaced with a proper client for OpenAI, Anthropic, etc.
/// </summary>
class MockChatClient : IChatClient, IAsyncDisposable
{
    private readonly ILogger? _logger;
    private List<ToolCall>? _toolCalls;

    public MockChatClient(ILogger? logger = null)
    {
        _logger = logger;
    }

    public IReadOnlyList<ToolCall>? ToolCalls => _toolCalls;

    public async Task<string> GetResponseAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Mock chat client received request with {Count} messages", messages.Count);

        // In a real implementation, this would call the AI provider's API
        await Task.Delay(100, cancellationToken); // Simulate API call

        // For demonstration, if the message contains a question about days,
        // simulate a tool call to the time tool
        var lastMessage = messages.LastOrDefault()?.Text ?? string.Empty;
        if (lastMessage.Contains("days") && options?.Tools?.Any(t => t.Name == "time") == true)
        {
            _toolCalls = new List<ToolCall>
                {
                    new ToolCall
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "time",
                        Arguments = JsonSerializer.SerializeToDocument(new { format = "short" }),
                        ReturnValue = "There are 9,208 days between those dates."
                    }
                };

            return "Based on the calculation, there are 9,208 days between January 1, 2000, and March 18, 2025.";
        }

        return "I don't have enough information to answer that question.";
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
*/