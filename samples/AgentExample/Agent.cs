using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentExample;

/// <summary>
/// Represents an Agent that can interact with tools and AI models.
/// </summary>
public class Agent // : IAsyncDisposable
{
    /*
    private readonly IList<McpServerStdio> _mcpServers;
    private readonly IList<IMcpClient> _clients = new List<IMcpClient>();
    private readonly ILogger? _logger;
    */
    private readonly string modelId;
    private readonly string systemPrompt;
    private readonly List<ITool> tools;
    private readonly IChatClient chatClient;

    public Agent(string modelId, string systemPrompt, List<ITool> tools, IChatClient chatClient)
    {
        this.modelId = modelId;
        this.systemPrompt = systemPrompt;
        this.tools = tools;
        this.chatClient = chatClient;
    }

    /// <summary>
    /// Gets the ID of the AI model being used.
    /// </summary>
    public string ModelId => modelId;

    public async Task<ChatResponse> Chat(string message, ChatOptions chatOptions)
    {
        // Call the AI model with the provided input and options
        /*
        var result = await this.client.GetResponseAsync(new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, message)
        }, chatOptions);

        return result;
        */
        List<ChatMessage> messages = [];
        messages.Add(new(ChatRole.System, systemPrompt));
        messages.Add(new(ChatRole.User, message));

        Console.WriteLine($"Options: {string.Join(",", chatOptions.Tools?.Select(t => t.Name) ?? new List<string> { "EMPTY"})}");

        List<ChatResponseUpdate> updates = [];
        var _tools = chatOptions.Tools ?? new List<AITool>();
        if (_tools.Any() == false)
        {
            Console.WriteLine("No tools available");
        }
        await foreach (var update in this.chatClient.GetStreamingResponseAsync(messages, new ChatOptions { Tools = [.. _tools] }, CancellationToken.None))
        {
            // Process each update as it arrives
            if (update is ChatResponseUpdate chatUpdate)
            {
                updates.Add(chatUpdate);
                Console.WriteLine(chatUpdate.ToString());
            }
        }

        // Combine updates into a single response
        var finalMessage = string.Join("", updates.Select(u => u.Contents.ToString())); 
        var finalChatMessage = new ChatMessage(ChatRole.Assistant, finalMessage);

        // Add the final message to the conversation
        messages.Add(finalChatMessage);

        // Return a ChatResponse object
        return new ChatResponse(messages);
    }

    /* 
        /// <summary>
        /// Gets the MCP servers associated with this agent.
        /// </summary>
        public IReadOnlyList<McpServerStdio> McpServers => _mcpServers.ToList().AsReadOnly();

        /// <summary>
        /// Gets the MCP clients connected to the servers.
        /// </summary>
        public IReadOnlyList<IMcpClient> Clients => _clients.ToList().AsReadOnly();

        /// <summary>
        /// Adds an MCP server to the agent.
        /// </summary>
        /// <param name="server">The server to add.</param>
        public void AddMcpServer(McpServerStdio server)
        {
            _mcpServers.Add(server ?? throw new ArgumentNullException(nameof(server)));
        }

        /// <summary>
        /// Creates an asynchronous scope that starts all the MCP servers.
        /// </summary>
        /// <returns>An <see cref="AsyncScope"/> that manages the lifetime of the MCP servers.</returns>
        public AsyncScope RunMcpServers(Dictionary<string, string> transportOptions)
        {
            return new AsyncScope(async () =>
            {
                // Start all the MCP servers
                _logger?.LogInformation("Starting {Count} MCP servers", _mcpServers.Count);

                List<Task> startTasks = new();
                foreach (var server in _mcpServers)
                {
                    startTasks.Add(server.StartAsync());
                }

                // Wait for all servers to start
                await Task.WhenAll(startTasks);
                _logger?.LogInformation("All MCP servers started successfully");

                // Connect clients to each server
                foreach (var server in _mcpServers)
                {
                    var client = await McpClientFactory.CreateAsync(new()
                    {
                        Id = "Agent",
                        Name = "Agent",
                        TransportType = TransportTypes.StdIo,
                        TransportOptions = transportOptions
                    });

                    _clients.Add(client);
                }
            }, 
            async () =>
            {
                // Cleanup clients
                foreach (var client in _clients)
                {
                    if (client is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync();
                    }
                }
                _clients.Clear();

                // Cleanup servers
                foreach (var server in _mcpServers)
                {
                    await server.DisposeAsync();
                }

                _logger?.LogInformation("All MCP servers stopped");
            });
        }

        /// <summary>
        /// Runs a prompt against the AI model with the connected MCP servers providing tools.
        /// </summary>
        /// <param name="prompt">The prompt to send to the model.</param>
        /// <param name="options">Optional ChatOptions to configure the request.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The result from the AI model.</returns>
        public async Task<AgentResult> RunAsync(string prompt, ChatOptions? options = null,  CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation($"Running prompt with model {ModelId}: {prompt}", modelId, prompt);

            // Prepare chat messages
            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, prompt)
            };

            // Prepare options with tools from all connected MCP clients
            var chatOptions = options ?? new ChatOptions();
            chatOptions.Tools ??= new List<AITool>();

            // Add tools from all clients
            foreach (var client in _clients)
            {
                var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
                foreach (var tool in tools)
                {
                    chatOptions.Tools.Add(tool);
                }
            }

            // Call LLM model with Microsoft.Extensions.AI.OpenAI package and send the result back.
            var chatClient = new OpenAIClient(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
                .AsChatClient(ModelId)
                .AsBuilder()
                .UseFunctionInvocation()
                .Build();

            var result = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);

            return new AgentResult
            {
                Data = result,
                ToolCalls = chatOptions.Tools
            };
        }

        /// <summary>
        /// Disposes resources used by the agent.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            // Dispose all clients
            foreach (var client in _clients)
            {
                if (client is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
            }
            _clients.Clear();

            // Dispose all servers
            foreach (var server in _mcpServers)
            {
                await server.DisposeAsync();
            }
        }
        */
}

/// <summary>
/// Represents the result of an agent operation.
/// </summary>
public class AgentResult
{
    /// <summary>
    /// Gets or sets the response data from the model.
    /// </summary>
    public string? Data { get; set; }
    
    /// <summary>
    /// Gets or sets the list of tool calls made during the operation.
    /// </summary>
    public IList<AITool> ToolCalls { get; set; } = [];
}

/// <summary>
/// Represents an asynchronous operation scope with setup and cleanup.
/// </summary>
public class AsyncScope : IAsyncDisposable
{
    private readonly Func<Task> _cleanup;
    
    /// <summary>
    /// Creates a new instance of <see cref="AsyncScope"/> with the specified setup and cleanup actions.
    /// </summary>
    /// <param name="setup">The setup action to run when the scope is created.</param>
    /// <param name="cleanup">The cleanup action to run when the scope is disposed.</param>
    public AsyncScope(Func<Task> setup, Func<Task> cleanup)
    {
        _cleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
        _ = setup?.Invoke() ?? Task.CompletedTask;
    }
    
    /// <summary>
    /// Performs cleanup when the scope is disposed.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _cleanup();
    }
}

[JsonSerializable(typeof(ChatOptions))]
public partial class SerializationContext : JsonSerializerContext
{
}