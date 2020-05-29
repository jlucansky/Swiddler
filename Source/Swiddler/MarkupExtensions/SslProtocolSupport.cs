using System;
using System.Security.Authentication;
using System.Windows.Markup;

namespace Swiddler.MarkupExtensions
{
    public class SslProtocolSupport : MarkupExtension
    {
        public string Name { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Name)) return null;
            return Enum.TryParse<SslProtocols>(Name, out _);
        }
    }
}
