using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Windows.AppLifecycle;

namespace BlueBattery.Diagnostics;

internal static class SingleInstanceDiagnostics
{
    private static readonly object SyncRoot = new();

    internal static string LogFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "blue_battery",
            "logs",
            "single-instance.log");

    internal static void Log(string stage, AppActivationArguments? activationArgs = null, AppInstance? keyInstance = null, Exception? exception = null)
    {
        try
        {
            string logDirectory = Path.GetDirectoryName(LogFilePath) ?? AppContext.BaseDirectory;
            Directory.CreateDirectory(logDirectory);

            StringBuilder builder = new();
            builder.Append('[')
                .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
                .Append("] ")
                .Append(stage)
                .AppendLine();
            builder.Append("  OS PID=").Append(Environment.ProcessId).AppendLine();
            builder.Append("  Thread=").Append(Environment.CurrentManagedThreadId).AppendLine();

            try
            {
                AppInstance current = AppInstance.GetCurrent();
                builder.Append("  CurrentInstance: ProcessId=")
                    .Append(current.ProcessId)
                    .Append(", Key=")
                    .Append('\'').Append(current.Key ?? string.Empty).Append('\'')
                    .Append(", IsCurrent=")
                    .Append(current.IsCurrent)
                    .AppendLine();
            }
            catch (Exception currentException)
            {
                builder.Append("  CurrentInstanceError=").Append(currentException.GetType().Name).Append(": ").Append(currentException.Message).AppendLine();
            }

            if (keyInstance is not null)
            {
                builder.Append("  KeyInstance: ProcessId=")
                    .Append(keyInstance.ProcessId)
                    .Append(", Key=")
                    .Append('\'').Append(keyInstance.Key ?? string.Empty).Append('\'')
                    .Append(", IsCurrent=")
                    .Append(keyInstance.IsCurrent)
                    .AppendLine();
            }

            if (activationArgs is not null)
            {
                builder.Append("  ActivationKind=")
                    .Append(activationArgs.Kind)
                    .AppendLine();
            }

            try
            {
                IList<AppInstance> instances = AppInstance.GetInstances();
                builder.Append("  Instances(").Append(instances.Count).AppendLine("):");
                foreach (AppInstance instance in instances)
                {
                    builder.Append("    - ProcessId=")
                        .Append(instance.ProcessId)
                        .Append(", Key=")
                        .Append('\'').Append(instance.Key ?? string.Empty).Append('\'')
                        .Append(", IsCurrent=")
                        .Append(instance.IsCurrent)
                        .AppendLine();
                }
            }
            catch (Exception instancesException)
            {
                builder.Append("  InstancesError=").Append(instancesException.GetType().Name).Append(": ").Append(instancesException.Message).AppendLine();
            }

            if (exception is not null)
            {
                builder.Append("  Exception=").Append(exception).AppendLine();
            }

            builder.AppendLine();

            lock (SyncRoot)
            {
                File.AppendAllText(LogFilePath, builder.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics must never break app startup.
        }
    }
}
