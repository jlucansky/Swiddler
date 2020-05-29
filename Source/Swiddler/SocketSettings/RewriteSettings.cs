using Swiddler.Channels;
using Swiddler.Common;
using Swiddler.IO;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Swiddler.SocketSettings
{
    public class RewriteSettings : SettingsBase
    {
        public event EventHandler<bool> BinaryChanged;

        public RewriteSettings()
        {
            Caption = "Data Rewrite";
            ImageName = "Edit";
        }

        protected string _MatchData = "Host: localhost:1337", _ReplaceData = "Host: example.org";
        [XmlIgnore] public string MatchData { get => _MatchData; set => SetProperty(ref _MatchData, value); }
        [XmlIgnore] public string ReplaceData { get => _ReplaceData; set => SetProperty(ref _ReplaceData, value); }


        [XmlElement(nameof(MatchData))] public string MatchDataEncoded
        {
            get => Convert.ToBase64String(Constants.UTF8Encoding.GetBytes(MatchData));
            set => MatchData = Constants.UTF8Encoding.GetString(Convert.FromBase64String(value));
        }

        [XmlElement(nameof(ReplaceData))] public string ReplaceDataEncoded
        {
            get => Convert.ToBase64String(Constants.UTF8Encoding.GetBytes(ReplaceData));
            set => ReplaceData = Constants.UTF8Encoding.GetString(Convert.FromBase64String(value));
        }

        protected bool _Outbound = true, _Inbound = true;
        public bool Outbound { get => _Outbound; set { SetProperty(ref _Outbound, value); } }
        public bool Inbound { get => _Inbound; set { SetProperty(ref _Inbound, value); } }


        protected bool _Binary;
        public bool Binary { get => _Binary; set { if (SetProperty(ref _Binary, value)) BinaryChanged?.Invoke(this, value); ; } }


        public bool TryGetMatchBytes(out byte[] data) => TryGetBytes(MatchData, out data);
        public bool TryGetReplaceBytes(out byte[] data) => TryGetBytes(ReplaceData, out data);

        private bool TryGetBytes(string source, out byte[] data)
        {
            if (Binary) 
                return TryParseHex(source, out data);
            
            data = Encoding.Default.GetBytes(source);
            return true;
        }

        public void Normalize()
        {
            if (Binary)
            {
                byte[] data;
                if (TryGetBytes(_MatchData, out data)) _MatchData = GetHex(data);
                if (TryGetBytes(_ReplaceData, out data)) _ReplaceData = GetHex(data);
            }
        }

        static string GetHex(byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", " ");
        }

        public static bool TryParseHex(string source, out byte[] data)
        {
            var list = new List<byte>(source.Length / 3);
            try
            {
                foreach (var g in source.Split(new[] { ' ', '-', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    foreach (var item in Split(g, 2))
                        list.Add(Convert.ToByte(item, 0x10));
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                data = list.ToArray();
            }
        }

        private static IEnumerable<string> Split(string str, int chunkSize)
        {
            int count = str.Length / chunkSize;

            if (count * chunkSize < str.Length) count++;

            for (int i = 0; i < count; i++)
            {
                yield return str.Substring(i * chunkSize, chunkSize);
            }
        }

        public static RewriteRule[] GetRewriteRules(IEnumerable<RewriteSettings> rewrites)
        {
            var result = new List<RewriteRule>();
            foreach (var item in rewrites)
            {
                if (!item.TryGetMatchBytes(out var matchBytes)) throw new ArgumentException();
                if (!item.TryGetReplaceBytes(out var replacebytes)) throw new ArgumentException();

                if (item.Inbound) result.Add(new RewriteRule()
                {
                    Search = matchBytes,
                    Replace = replacebytes,
                    Flow = TrafficFlow.Inbound,
                });

                if (item.Outbound) result.Add(new RewriteRule()
                {
                    Search = matchBytes,
                    Replace = replacebytes,
                    Flow = TrafficFlow.Outbound,
                });

            }
            return result.ToArray();
        }

        public static RewriteSettings DesignInstance => new RewriteSettings();
    }
}
