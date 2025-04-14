using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol.Types;
using System.Diagnostics;

namespace ModelContextProtocol.Server;

/// <summary>
/// An MCP server that communicates with an external process using standard input/output.
/// </summary>
public class McpServerStdio : IDisposable, IAsyncDisposable
{
    private readonly string _command;
    private readonly string[] _args;
    private readonly McpServerOptions _options;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly IServiceProvider? _serviceProvider;
    private readonly ILogger? _logger;
    private Process? _process;
    private IMcpServer? _server;
    private bool _isDisposed;

    /// <summary>
    /// Creates a new instance of <see cref="McpServerStdio"/>.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="args">The arguments to pass to the command.</param>
    /// <param name="options">Optional configuration options for the server.</param>
    /// <param name="loggerFactory">Optional logger factory to use for logging.</param>
    /// <param name="serviceProvider">Optional service provider to use for dependency injection.</param>
    public McpServerStdio(
        string command,
        string[] args,
        McpServerOptions? options = null,
        ILoggerFactory? loggerFactory = null,
        IServiceProvider? serviceProvider = null)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));
        _args = args ?? Array.Empty<string>();
        _options = options ?? new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = $"External Process ({command})",
                Version = "1.0.0"
            }
        };
        _loggerFactory = loggerFactory;
        _serviceProvider = serviceProvider;
        _logger = loggerFactory?.CreateLogger<McpServerStdio>();
    }

    /// <summary>
    /// Gets the underlying MCP server instance.
    /// </summary>
    public IMcpServer? Server => _server;

    /// <summary>
    /// Gets the associated process.
    /// </summary>
    public Process? Process => _process;

    /// <summary>
    /// Starts the external process and connects the MCP server to it.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(McpServerStdio));
        if (_process != null) throw new InvalidOperationException("Process is already started.");

        _logger?.LogInformation("Starting external process: {Command} {Args}", _command, string.Join(" ", _args));
        
        var processStartInfo = new ProcessStartInfo
        {
            FileName = _command,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var arg in _args)
        {
            processStartInfo.ArgumentList.Add(arg);
        }

        _process = new Process
        {
            StartInfo = processStartInfo,
            EnableRaisingEvents = true
        };

        _process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                _logger?.LogWarning("Process stderr: {Data}", e.Data);
            }
        };

        try
        {
            if (!_process.Start())
            {
                throw new InvalidOperationException($"Failed to start process: {_command}");
            }

            _process.BeginErrorReadLine();

            // Create transport for communication with the process
            var transport = new StdioServerTransport($"External Process ({_command})")
            {
                //StandardInput = _process.StandardInput,
                //StandardOutput = _process.StandardOutput
            };

            // Create the MCP server with the transport
            _server = McpServerFactory.Create(transport, _options, _loggerFactory, _serviceProvider);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start process or create MCP server");
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Runs the MCP server with the external process.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(McpServerStdio));
        if (_server == null) await StartAsync(cancellationToken).ConfigureAwait(false);

        // The MCP server should be initialized by now
        await _server!.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes of resources used by the MCP server and external process.
    /// </summary>
    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            
            try
            {
                if (_server is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing MCP server");
            }

            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill(true);
                    _process.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error killing or disposing process");
            }
        }
    }

    /// <summary>
    /// Asynchronously disposes of resources used by the MCP server and external process.
    /// </summary>
    /// <returns>A task representing the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            
            try
            {
                if (_server is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (_server is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing MCP server");
            }

            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill(true);
                    _process.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error killing or disposing process");
            }
        }
    }
}