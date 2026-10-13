namespace MacChanger.Core;
public interface IWindowsCommandExecutor { Task<OperationResult> ExecuteAsync(string command,CancellationToken cancellationToken=default); }
