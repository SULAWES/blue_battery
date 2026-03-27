using BlueBattery.Models;
using System.Text.Json.Serialization;

namespace BlueBattery.Services.State;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppStateSnapshot))]
[JsonSerializable(typeof(DeviceBatteryInfo[]))]
[JsonSerializable(typeof(DeviceBatteryInfo))]
internal sealed partial class AppStateJsonContext : JsonSerializerContext
{
}
