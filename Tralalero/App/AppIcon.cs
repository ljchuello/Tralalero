using System.Drawing;
using System.Windows.Forms;

namespace Tralalero.App;

internal static class AppIcon
{
    public static Icon Load()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "tralalero.ico");
            if (File.Exists(path))
            {
                return new Icon(path);
            }
        }
        catch
        {
            // Fall through to the executable icon.
        }

        try
        {
            var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (icon is not null)
            {
                return icon;
            }
        }
        catch
        {
            // Fall through to the system icon.
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}
