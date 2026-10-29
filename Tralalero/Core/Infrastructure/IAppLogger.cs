namespace Tralalero.Core.Infrastructure;

/// <summary>Small logging abstraction so core services stay easy to test.</summary>
public interface IAppLogger
{
    void Info(string message);
    void Warn(string message, Exception? exception = null);
    void Error(string message, Exception? exception = null);
    void Debug(string message);
}

/// <summary>A logger that intentionally does nothing. Useful in tests and tools.</summary>
public sealed class NullAppLogger : IAppLogger
{
    public static readonly NullAppLogger Instance = new();

    private NullAppLogger()
    {
    }

    public void Info(string message)
    {
    }

    public void Warn(string message, Exception? exception = null)
    {
    }

    public void Error(string message, Exception? exception = null)
    {
    }

    public void Debug(string message)
    {
    }
}
