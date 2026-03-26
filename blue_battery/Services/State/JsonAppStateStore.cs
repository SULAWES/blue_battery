using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace BlueBattery.Services.State;

public sealed class JsonAppStateStore : IAppStateStore
{
    private const string StateFileName = "app-state.json";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public async Task<AppStateSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        string filePath = GetStateFilePath();
        if (!File.Exists(filePath))
        {
            return null;
        }

        await using FileStream stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<AppStateSnapshot>(stream, SerializerOptions, cancellationToken);
    }

    public async Task SaveAsync(AppStateSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        string filePath = GetStateFilePath();
        string directoryPath = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException("无法确定状态文件目录。");
        Directory.CreateDirectory(directoryPath);

        await using FileStream stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, cancellationToken);
    }

    private static string GetStateFilePath()
    {
        return Path.Combine(ApplicationData.Current.LocalFolder.Path, StateFileName);
    }
}
