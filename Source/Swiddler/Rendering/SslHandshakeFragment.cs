using Swiddler.Security;
using Swiddler.Serialization;
using Swiddler.Views.Fragments;
using System;
using System.Linq;

namespace Swiddler.Rendering
{
    public class SslHandshakeFragment : Fragment
    {
        public override Type VisualType => typeof(SslHandshakeView);

        public SslHandshake Model { get; set; }

        public string IconName => Model.IsValid() ? "LockValid" : "LockInvalid";

        public string ToolTip
        {
            get
            {
                var chain = Model.GetPrimaryCertificateChain();
                if (chain?.Any() != true) return null;
                string result = "";
                int level = 0;
                foreach (var crt in chain.Reverse())
                {
                    if (level > 0) result += $"{Environment.NewLine}{new string(' ', (level - 1) * 4)}▶ ";
                    result += crt.GetCertDisplayName();
                    level++;
                }
                return result;
            }
        }
    }
}
