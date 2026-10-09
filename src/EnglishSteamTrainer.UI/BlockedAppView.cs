using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.UI;

internal sealed record BlockedAppView(string Name, ImageSource? Icon)
{
    public static List<BlockedAppView> From(IEnumerable<BlockedApp> apps)
    {
        return apps.Select(app => new BlockedAppView(app.Name, TryLoadIcon(app.Name))).ToList();
    }

    // Icons/steam.png usw. - neue Programme aus der Online-Liste ohne eigenes Icon
    // werden einfach mit Namen angezeigt.
    private static ImageSource? TryLoadIcon(string name)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/Icons/{name.ToLowerInvariant()}.png");
            var resource = Application.GetResourceStream(uri);

            if (resource is null)
                return null;

            using var stream = resource.Stream;
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = stream;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
