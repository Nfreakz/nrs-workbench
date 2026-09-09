namespace NRS.Workbench.Core.Interfaces;

public interface ILogReaderService
{
    string ReadLatest(string runnerFolder, int maxLines = 250);
}
