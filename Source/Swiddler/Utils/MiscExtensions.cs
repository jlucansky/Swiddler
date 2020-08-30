using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace Swiddler.Utils
{
    public static class MiscExtensions
    {
        public static bool IsBinary(this byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];

                if (b == '\t') continue;
                if (b == '\r') continue;
                if (b == '\n') continue;
                
                if (b < 32) return true;
            }

            return false;
        } 
       
        static IEnumerable<string> FormatHexIterator(this byte[] data)
        {
            int blockSize = 4;
            for (int i = 0; i < data.Length; i += blockSize)
            {
                yield return BitConverter.ToString(data, i, Math.Min(blockSize, data.Length - i)).Replace("-", " ");
            }
        }

        public static string FormatHex(this byte[] data)
        {
            var sb = new StringBuilder();
            int c = 0;
            foreach (var group in FormatHexIterator(data))
            {
                sb.Append(group);
                sb.Append("  ");

                c++;

                if (c == 4)
                {
                    sb.Append(" ");
                }
                if (c == 8)
                {
                    sb.AppendLine();
                    c = 0;
                }
            }
            return sb.ToString();
        }

        static readonly char[] hexSeparators = new[] { '\r', '\n', '\t', ' ', '-', ',' };
        public static void TokenizeHex(this string text, out byte[] data, out int[] locations)
        {
            var dataList = new List<byte>(text.Length / 3);
            var locList = new List<int>(text.Length / 3);

            string token = ""; // current token
            int pos = 0;

            try
            {
                void ParseNext()
                {
                    if (token.Length == 0) return;
                    dataList.Add(Convert.ToByte(token, 0x10));
                    locList.Add(pos);
                    token = "";
                }

                for (int i = 0; i < text.Length; i++)
                {
                    var ch = text[i];

                    if (hexSeparators.Contains(ch))
                    {
                        ParseNext();
                        pos = i + 1;
                        continue;
                    }

                    token += ch;

                    if (token.Length == 2)
                    {
                        ParseNext();
                        pos = i + 1;
                    }
                }

                ParseNext();
            }
            catch
            {
                throw new FormatException($"Invalid character: '{token}' at position {pos}.");
            }

            data = dataList.ToArray();
            locations = locList.ToArray();
        }

        const string Swiddler_Selection = nameof(Swiddler_Selection);
        public static void SetSwiddlerSelection(this IDataObject dataObject)
        {
            dataObject.SetData(Swiddler_Selection, 1);
        }

        public static bool ContainsSwiddlerSelection(this IDataObject dataObject)
        {
            return dataObject.GetDataPresent(Swiddler_Selection);
        }

        public static void InsertRange<T>(this IList<T> list, int index, IEnumerable<T> collection)
        {
            foreach (var item in collection)
                list.Insert(index++, item);
        }

        public static void RemoveRange<T>(this IList<T> list, int index, int count)
        {
            for (int i = 0; i < count; i++)
                list.RemoveAt(index);
        }
    }
}
