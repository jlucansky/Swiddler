using System.Windows;
using System.Windows.Controls;

namespace Swiddler.Controls
{
	// TODO: prerobit podla https://github.com/microsoft/WPF-Samples/blob/master/PerMonitorDPI/ImageScaling/DpiAwareImage.cs

	public class CrispImage : Image
    {
        static CrispImage()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CrispImage), new FrameworkPropertyMetadata(typeof(CrispImage)));
        }


		public static readonly DependencyProperty ImageNameProperty =
			DependencyProperty.Register(nameof(ImageName), typeof(string), typeof(CrispImage), new FrameworkPropertyMetadata(default(string)));


		public string ImageName
		{
			get => (string)GetValue(ImageNameProperty);
			set => SetValue(ImageNameProperty, value);
		}


		/// <summary>
		/// Dpi attached property
		/// </summary>
		public static readonly DependencyProperty DpiProperty =
			DependencyProperty.RegisterAttached("Dpi", typeof(double), typeof(CrispImage),
			new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.Inherits));

		/// <summary>
		/// Gets the dpi
		/// </summary>
		/// <param name="depo">Object</param>
		/// <returns></returns>
		public static double GetDpi(DependencyObject depo) => (double)depo.GetValue(DpiProperty);

		/// <summary>
		/// Sets the dpi
		/// </summary>
		/// <param name="depo">Object</param>
		/// <param name="value">Value</param>
		/// <returns></returns>
		public static void SetDpi(DependencyObject depo, double value) => depo.SetValue(DpiProperty, value);


		/// <summary>
		/// DpiScale attached property
		/// </summary>
		public static readonly DependencyProperty DpiScaleProperty =
			DependencyProperty.RegisterAttached("DpiScale", typeof(double), typeof(CrispImage),
			new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.Inherits));

		/// <summary>
		/// Gets the DpiScale
		/// </summary>
		/// <param name="depo">Object</param>
		/// <returns></returns>
		public static double GetDpiScale(DependencyObject depo) => (double)depo.GetValue(DpiScaleProperty);

		/// <summary>
		/// Sets the DpiScale
		/// </summary>
		/// <param name="depo">Object</param>
		/// <param name="value">Value</param>
		/// <returns></returns>
		public static void SetDpiScale(DependencyObject depo, double value) => depo.SetValue(DpiScaleProperty, value);

		/// <summary>
		/// OneByDpiScale attached property
		/// </summary>
		public static readonly DependencyProperty OneByDpiScaleProperty =
			DependencyProperty.RegisterAttached("OneByDpiScale", typeof(double), typeof(CrispImage),
			new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.Inherits));

		/// <summary>
		/// Gets the OneByDpiScale
		/// </summary>
		/// <param name="depo">Object</param>
		/// <returns></returns>
		public static double GetOneByDpiScale(DependencyObject depo) => (double)depo.GetValue(OneByDpiScaleProperty);

		/// <summary>
		/// Sets the OneByDpiScale
		/// </summary>
		/// <param name="depo">Object</param>
		/// <param name="value">Value</param>
		/// <returns></returns>
		public static void SetOneByDpiScale(DependencyObject depo, double value) => depo.SetValue(OneByDpiScaleProperty, value);

	}
}
