using Swiddler.Common;
using System.Windows.Media;

namespace Swiddler.ViewModels
{
    public class ImageButton : BindableBase
    {
        string _Text;
        public string Text { get => _Text; set => SetProperty(ref _Text, value); }


        ImageSource _Image;
        public ImageSource Image { get => _Image; set => SetProperty(ref _Image, value); }


        string _ImageName;
        public string ImageName { get => _ImageName; set => SetProperty(ref _ImageName, value); }

        public override string ToString() => _Text;
    }
}
