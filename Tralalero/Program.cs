using System.Net;
using Tralalero.App;
using Tralalero.Core.Infrastructure;
using Tralalero.Core.Interop;
using Tralalero.Core.Security;
using Tralalero.Core.Settings;

namespace Tralalero;

internal static class Program
{
    private const string MutexName = @"Local\Tralalero.SingleInstance";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            if (!SingleInstance.ActivateExistingWindow(UiText.WindowTitle))
            {
                MessageBox.Show(
                    "Tralalero is already running. Look for its microphone icon in the notification area.",
                    "Tralalero",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppPaths.EnsureDirectories();

        var logger = new AppLogger(AppPaths.LogsDirectory);
        logger.Info("Tralalero starting.");

        Application.ThreadException += (_, args) =>
        {
            logger.Error("Unhandled UI exception.", args.Exception);
            ShowFatalError(args.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                logger.Error("Unhandled application exception.", exception);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.Warn("Unobserved task exception.", args.Exception);
            args.SetObserved();
        };

        try
        {
            var settingsService = new SettingsService(AppPaths.SettingsFile, logger);
            var keyStore = new ApiKeyStore(AppPaths.CredentialsFile, logger);

            using var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            };
            using var httpClient = new HttpClient(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };

            Application.Run(new MainForm(settingsService, keyStore, logger, httpClient));
        }
        catch (Exception ex)
        {
            logger.Error("Tralalero failed to start.", ex);
            ShowFatalError(ex);
        }
        finally
        {
            logger.Info("Tralalero stopped.");
            try
            {
                mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The mutex may not be owned if startup failed before Run.
            }
        }
    }

    private static void ShowFatalError(Exception exception)
    {
        try
        {
            MessageBox.Show(
                $"Something went wrong.\n\n{exception.Message}\n\n" +
                $"Logs: {AppPaths.LogsDirectory}",
                "Tralalero",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            // There is nothing else we can do.
        }
    }
}
