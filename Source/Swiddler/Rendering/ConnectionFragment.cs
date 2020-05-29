using Swiddler.Common;
using Swiddler.Views.Fragments;
using System;

namespace Swiddler.Rendering
{
    public class ConnectionFragment : Fragment
    {
        public override Type VisualType => typeof(ConnectionView);

        public ConnectionBanner Model { get; set; }
    }
}
