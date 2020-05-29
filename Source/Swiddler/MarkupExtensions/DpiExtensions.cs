using System;
using System.Windows;
using System.Windows.Markup;

namespace Swiddler.MarkupExtensions
{
    public class Dpi : MarkupExtension
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => App.Current?.Res.Dpi ?? 96;
    }

    public class DpiScale : MarkupExtension
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => App.Current?.Res.DpiScale ?? 1;
    }

    public class OneByDpiScale : MarkupExtension
    {
        public bool AsThickness { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var val = App.Current?.Res.OneByDpiScale ?? 1;

            if (AsThickness)
                return new Thickness(val);
            return val;
        }
    }

}
