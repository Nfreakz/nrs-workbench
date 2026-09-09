using System.Text;
using NRS.Workbench.Core.Interfaces;

namespace NRS.Workbench.App.Services;

public sealed class LogReaderService : ILogReaderService
{
    public string ReadLatest(string runnerFolder, int maxLines = 250)
    {
        try
        {
            var diag = Path.Combine(runnerFolder, "_diag");
            if (!Directory.Exists(diag)) return "No hay carpeta _diag para este runner.";

            var file = new DirectoryInfo(diag)
                .EnumerateFiles("*.log", SearchOption.TopDirectoryOnly)
                .OrderByDescending(x => x.LastWriteTimeUtc)
                .FirstOrDefault();
            if (file is null) return "No hay logs disponibles.";

            // GitHub Runner keeps the active log open while it is writing to it.
            // Open it explicitly with sharing enabled so the manager can tail/read
            // a live runner without fighting the runner process for the file lock.
            using var stream = new FileStream(
                file.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                detectEncodingFromByteOrderMarks: true);

            var queue = new Queue<string>(maxLines);
            while (reader.ReadLine() is { } line)
            {
                if (queue.Count == maxLines) queue.Dequeue();
                queue.Enqueue(line);
            }

            return queue.Count == 0
                ? "El log está vacío por ahora."
                : string.Join(Environment.NewLine, queue);
        }
        catch (IOException ex)
        {
            AppLogger.Error($"Could not read logs for '{runnerFolder}'", ex);
            return "El log está temporalmente ocupado. Se reintentará en la próxima actualización.";
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLogger.Error($"Access denied reading logs for '{runnerFolder}'", ex);
            return "No se puede leer el log por permisos. Ejecuta NRS Workbench como administrador.";
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Could not read logs for '{runnerFolder}'", ex);
            return $"No se pudo leer el log: {ex.Message}";
        }
    }
}
