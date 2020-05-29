using Swiddler.Common;
using Swiddler.Controls;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Swiddler
{
    public class AppResources : BindableBase
    {
        double _DpiScale = 1;
        public double DpiScale { get => _DpiScale; set => SetProperty(ref _DpiScale, value); }

        double _Dpi;
        public double Dpi { get => _Dpi; set => SetProperty(ref _Dpi, value); }

        double _OneByDpiScale;
        public double OneByDpiScale { get => _OneByDpiScale; set => SetProperty(ref _OneByDpiScale, value); }

        public SolidColorBrush InboundFlowBrush { get; } = FindBrushResource("Swiddler.Flow.Inbound");
        public SolidColorBrush OutboundFlowBrush { get; } = FindBrushResource("Swiddler.Flow.Outbound");

        public SolidColorBrush InboundSelectedFlowBrush { get; } = FindBrushResource("Swiddler.Flow.Inbound.Selected");
        public SolidColorBrush OutboundSelectedFlowBrush { get; } = FindBrushResource("Swiddler.Flow.Outbound.Selected");


        public SolidColorBrush InboundFlowStrokeBrush { get; } = FindBrushResource("Swiddler.Flow.Inbound.Stroke");
        public SolidColorBrush OutboundFlowStrokeBrush { get; } = FindBrushResource("Swiddler.Flow.Outbound.Stroke");

        public SolidColorBrush InboundFlowTextBrush { get; } = FindBrushResource("Swiddler.Flow.Inbound.Text");
        public SolidColorBrush OutboundFlowTextBrush { get; } = FindBrushResource("Swiddler.Flow.Outbound.Text");

        public SolidColorBrush MessageInfoBrush { get; } = FindBrushResource("Swiddler.Message.Info.Brush");
        public SolidColorBrush MessageErrorBrush { get; } = FindBrushResource("Swiddler.Message.Error.Brush");
        public SolidColorBrush MessageSuccessBrush { get; } = FindBrushResource("Swiddler.Message.Success.Brush");

        public SolidColorBrush SelectionBrush { get; } = FindBrushResource("Swiddler.Selection.Brush");
        public SolidColorBrush SelectionStroke { get; } = FindBrushResource("Swiddler.Selection.Stroke");


        public Pen InboundFlowPen { get; private set; }
        public Pen OutboundFlowPen { get; private set; }
        public Pen SelectionPen { get; private set; }

        protected static SolidColorBrush FindBrushResource(object resourceKey) => (SolidColorBrush)App.Current.FindResource(resourceKey);

        public AppResources()
        {
            UpdateDpi();
        }

        public void AttachDpiProperties(FrameworkElement element)
        {
            element.SetBinding(CrispImage.DpiProperty, new Binding(nameof(Dpi)) { Source = this });
            element.SetBinding(CrispImage.DpiScaleProperty, new Binding(nameof(DpiScale)) { Source = this });
            element.SetBinding(CrispImage.OneByDpiScaleProperty, new Binding(nameof(OneByDpiScale)) { Source = this });
        }

        void UpdateDpi()
        {
            Dpi = (int)typeof(SystemParameters).GetProperty("Dpi", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null, null);
            DpiScale = Dpi / 96.0;
            OneByDpiScale = 1 / DpiScale;

            InboundFlowPen = new Pen(InboundFlowStrokeBrush, 1 / DpiScale);
            OutboundFlowPen = new Pen(OutboundFlowStrokeBrush, 1 / DpiScale);

            SelectionPen = new Pen(SelectionStroke, 1 / DpiScale);
        }
    }

}
