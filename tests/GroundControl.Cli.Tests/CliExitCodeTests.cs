using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace GroundControl.Cli.Tests;

/// <summary>
/// Locks in the contract that a CLI handler returning a non-zero exit code surfaces
/// to the OS as a non-zero process exit code. Regression coverage for the bug where
/// `await builder.RunAsync()` in Program.cs silently dropped the int, leaving the
/// process exit at 0 even when the API returned 400/409/422 and the handler returned 1.
/// </summary>
public sealed class CliExitCodeTests
{
    private static readonly string RepositoryRoot = ResolveRepositoryRoot();
    private static readonly string CliProjectPath = Path.Combine(RepositoryRoot, "src", "GroundControl.Cli", "GroundControl.Cli.csproj");

    [Fact]
    public async Task Cli_HandlerReturnsNonZero_ProcessExitCodeIsNonZero()
    {
        // Arrange: scope create with --no-interactive but no --dimension/--values forces
        // CreateScopeHandler to print an error and return 1 without any HTTP traffic.
        var args = new[] { "scope", "create", "--no-interactive", "--output", "json" };

        // Act
        var result = await RunCliAsync(args, serverUrl: null, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1, $"stdout:\n{result.Stdout}\nstderr:\n{result.Stderr}");
        (result.Stdout + result.Stderr).ShouldContain("Missing required option");
    }

    [Fact]
    public async Task Cli_ApiReturns400_ProcessExitCodeIsOne()
    {
        // Arrange
        using var server = StubHttpServer.Start(StubHttpServer.BadRequestValidationProblem);
        var args = new[] { "scope", "create", "--dimension", "tier", "--values", "dev,prod", "--no-interactive", "--output", "json" };

        // Act
        var result = await RunCliAsync(args, server.BaseUrl, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1, $"stdout:\n{result.Stdout}\nstderr:\n{result.Stderr}");
    }

    [Fact]
    public async Task Cli_ApiReturns409_ProcessExitCodeIsOne()
    {
        // Arrange
        using var server = StubHttpServer.Start(StubHttpServer.ConflictProblem);
        var args = new[] { "scope", "create", "--dimension", "tier", "--values", "dev,prod", "--no-interactive", "--output", "json" };

        // Act
        var result = await RunCliAsync(args, server.BaseUrl, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1, $"stdout:\n{result.Stdout}\nstderr:\n{result.Stderr}");
    }

    [Fact]
    public async Task Cli_ApiReturns422_ProcessExitCodeIsOne()
    {
        // Arrange
        using var server = StubHttpServer.Start(StubHttpServer.UnprocessableEntityProblem);
        var args = new[] { "scope", "create", "--dimension", "tier", "--values", "dev,prod", "--no-interactive", "--output", "json" };

        // Act
        var result = await RunCliAsync(args, server.BaseUrl, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1, $"stdout:\n{result.Stdout}\nstderr:\n{result.Stderr}");
    }

    private static async Task<ProcessOutcome> RunCliAsync(string[] args, string? serverUrl, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(CliProjectPath);
        startInfo.ArgumentList.Add("--");
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (serverUrl is not null)
        {
            startInfo.Environment["GroundControl__ServerUrl"] = serverUrl;
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start CLI process.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return new ProcessOutcome(process.ExitCode, stdout, stderr);
    }

    private static string ResolveRepositoryRoot()
    {
        var value = typeof(CliExitCodeTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "RepositoryRoot")?.Value;

        return value ?? throw new InvalidOperationException("RepositoryRoot assembly metadata not found.");
    }

    private sealed record ProcessOutcome(int ExitCode, string Stdout, string Stderr);

    private sealed class StubHttpServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;

        public string BaseUrl { get; }

        private StubHttpServer(HttpListener listener, string baseUrl, Func<HttpListenerContext, Task> handler)
        {
            _listener = listener;
            BaseUrl = baseUrl;
            _loop = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested && _listener.IsListening)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = await _listener.GetContextAsync().WaitAsync(_cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (HttpListenerException)
                    {
                        return;
                    }

                    try
                    {
                        await handler(context);
                    }
                    finally
                    {
                        context.Response.Close();
                    }
                }
            });
        }

        public static StubHttpServer Start(Func<HttpListenerContext, Task> handler)
        {
            // Pick a free port via a transient TcpListener so we don't collide.
            var port = GetFreeTcpPort();
            var prefix = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();
            return new StubHttpServer(listener, prefix.TrimEnd('/'), handler);
        }

        public static Func<HttpListenerContext, Task> BadRequestValidationProblem { get; } = WriteProblemAsync(
            statusCode: HttpStatusCode.BadRequest,
            contentType: "application/problem+json",
            body: JsonSerializer.Serialize(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                title = "One or more validation errors occurred.",
                status = 400,
                detail = "Validation failed.",
                errors = new Dictionary<string, string[]>
                {
                    ["Dimension"] = ["A scope with dimension 'tier' already exists."]
                }
            }));

        public static Func<HttpListenerContext, Task> ConflictProblem { get; } = WriteProblemAsync(
            statusCode: HttpStatusCode.Conflict,
            contentType: "application/problem+json",
            body: JsonSerializer.Serialize(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                title = "Conflict",
                status = 409,
                detail = "The resource was modified by another user."
            }));

        public static Func<HttpListenerContext, Task> UnprocessableEntityProblem { get; } = WriteProblemAsync(
            statusCode: HttpStatusCode.UnprocessableEntity,
            contentType: "application/problem+json",
            body: JsonSerializer.Serialize(new
            {
                type = "https://tools.ietf.org/html/rfc4918#section-11.2",
                title = "Unprocessable Entity",
                status = 422,
                detail = "The request could not be processed due to a business rule violation."
            }));

        private static Func<HttpListenerContext, Task> WriteProblemAsync(HttpStatusCode statusCode, string contentType, string body)
        {
            return async context =>
            {
                context.Response.StatusCode = (int)statusCode;
                context.Response.ContentType = contentType;
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
            };
        }

        private static int GetFreeTcpPort()
        {
            using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        }

        public void Dispose()
        {
            _cts.Cancel();
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed.
            }

            try
            {
                _loop.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
                // Background loop exited via cancellation.
            }

            _cts.Dispose();
        }
    }
}
