// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using Xunit;

namespace AzureMcp.Tests.Client.Helpers;

public class LiveTestFixture : IAsyncLifetime
{
    public LiveTestSettings Settings { get; private set; } = new();
    public IMcpClient Client { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        await InitializeSettingsAsync();
        await InitializeClientAsync();
    }

    private async Task InitializeClientAsync()
    {
        string testAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        string executablePath = OperatingSystem.IsWindows() ? Path.Combine(testAssemblyPath, "azmcp.exe") : Path.Combine(testAssemblyPath, "azmcp");

        StdioClientTransportOptions transportOptions = new()
        {
            Name = "Test Server",
            Command = executablePath,
            Arguments = ["server", "start"]
        };

        if (!string.IsNullOrEmpty(Settings.TestPackage))
        {
            Environment.CurrentDirectory = Settings.SettingsDirectory;
            transportOptions.Command = "npx";
            transportOptions.Arguments = ["-y", Settings.TestPackage, "server", "start"];
        }

        var clientTransport = new StdioClientTransport(transportOptions);

        Client = await McpClientFactory.CreateAsync(clientTransport);
    }

    private async ValueTask InitializeSettingsAsync()
    {
        var testSettingsFileName = ".testsettings.json";
        var directory = Path.GetDirectoryName(typeof(CommandTests).Assembly.Location);
        while (!string.IsNullOrEmpty(directory))
        {
            var testSettingsFilePath = Path.Combine(directory, testSettingsFileName);
            if (File.Exists(testSettingsFilePath))
            {
                var content = await File.ReadAllTextAsync(testSettingsFilePath);

                Settings = JsonSerializer.Deserialize<LiveTestSettings>(content)
                    ?? throw new Exception("Unable to deserialize live test settings");

                Settings.SettingsDirectory = directory;

                return;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new FileNotFoundException($"Test settings file '{testSettingsFileName}' not found in the assembly directory or its parent directories.");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
