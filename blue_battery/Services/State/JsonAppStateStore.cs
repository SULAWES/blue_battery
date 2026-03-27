using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace BlueBattery.Services.State;

public sealed class JsonAppStateStore : IAppStateStore
{
    private const string StateFileName = "app-state.json";
    public async Task<AppStateSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        string filePath = GetStateFilePath();
        if (!File.Exists(filePath))
        {
            return null;
        }

        await using FileStream stream = File.OpenRead(filePath);
        return await System.Text.Json.JsonSerializer.DeserializeAsync(
            stream,
            AppStateJsonContext.Default.AppStateSnapshot,
            cancellationToken);
    }

    public async Task SaveAsync(AppStateSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        string filePath = GetStateFilePath();
        string directoryPath = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException("无法确定状态文件目录。");
        Directory.CreateDirectory(directoryPath);

        await using FileStream stream = File.Create(filePath);
        await System.Text.Json.JsonSerializer.SerializeAsync(
            stream,
            snapshot,
            AppStateJsonContext.Default.AppStateSnapshot,
            cancellationToken);
    }

    private static string GetStateFilePath()
    {
        return Path.Combine(ApplicationData.Current.LocalFolder.Path, StateFileName);
    }
}
