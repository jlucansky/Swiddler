using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Swiddler.Utils
{
    /*
    public static class EncodingExtensions
    {
        public static byte[] GetEncodingTransform(this Encoding encoding, bool lower = false)
        {
            var sweep = new byte[0x100];
            for (int i = 0; i < 0x100; i++) sweep[i] = (byte)i;

            var text = encoding.GetString(sweep);
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder(260);

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            normalizedString = stringBuilder.ToString().Normalize(NormalizationForm.FormC);

            if (lower) normalizedString = normalizedString.ToLowerInvariant();

            return encoding.GetBytes(normalizedString);
        }

    }
    */
}
