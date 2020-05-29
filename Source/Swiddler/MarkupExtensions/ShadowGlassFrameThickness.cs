using System;
using System.Windows;
using System.Windows.Markup;

namespace Swiddler.MarkupExtensions
{
    public class ShadowGlassFrameThickness : MarkupExtension
    {
        static readonly Version shadowFromWinVer = new Version(6, 1); //  6.1 = Win7
        public override object ProvideValue(IServiceProvider serviceProvider) => GetThickness();

        public static Thickness GetThickness()
        {
            if (Environment.OSVersion.Version > shadowFromWinVer)
            {
                // TODO: on windows 10 there is white line at the bottom :(
                return new Thickness(0) { Bottom = App.Current?.Res.OneByDpiScale ?? 1 };
            }

            return new Thickness(0);
        }
    }
}
