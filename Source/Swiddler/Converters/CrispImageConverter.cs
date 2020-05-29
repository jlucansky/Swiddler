using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Swiddler.Converters
{
	public class CrispImageConverter : IMultiValueConverter
	{
		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();

		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values is null)
				throw new ArgumentNullException(nameof(values));

			if (values.Length != 4) return null;

			var width = (double)values[0];
			var height = (double)values[1];
			var name = (string)values[2];
			var dpi = (double)values[3];

			if (dpi == 0)
				dpi = 96;

			return GetImage(name, new Size(width, height), dpi);
		}

		static Size Round(Size size) => new Size(Math.Round(size.Width), Math.Round(size.Height));

		public BitmapSource GetImage(string name, Size size, double dpi)
		{
			if (name is null) return null;
			if (double.IsNaN(size.Width) || double.IsNaN(size.Height)) return null;

			var physicalSize = Round(new Size(size.Width * dpi / 96, size.Height * dpi / 96));

			if (physicalSize.Width == 0 || physicalSize.Height == 0)
				return null;

			ImageSourceInfo[] infos = null;
			if (imageResources?.TryGetValue(name, out infos) != true)
				return null;

			var infoList = new List<ImageSourceInfo>(infos);
			infoList.Sort((a, b) => {
				if (a.Size == b.Size)
					return 0;

				// Try exact size first
				if ((a.Size == physicalSize) != (b.Size == physicalSize))
					return a.Size == physicalSize ? -1 : 1;

				// Try any-size (xaml images)
				if ((a.Size == ImageSourceInfo.AnySize) != (b.Size == ImageSourceInfo.AnySize))
					return a.Size == ImageSourceInfo.AnySize ? -1 : 1;

				// Closest size (using height)
				if (a.Size.Height >= physicalSize.Height)
				{
					if (b.Size.Height < physicalSize.Height)
						return -1;
					return a.Size.Height.CompareTo(b.Size.Height);
				}
				else
				{
					if (b.Size.Height >= physicalSize.Height)
						return 1;
					return b.Size.Height.CompareTo(a.Size.Height);
				}
			});

			foreach (var info in infoList)
			{
				var bitmapSource = TryGetImage(info, physicalSize);
				if (!(bitmapSource is null))
					return bitmapSource;
			}
			
			return null;
		}

		static readonly Dictionary<ImageKey, BitmapSource> imageCache = new Dictionary<ImageKey, BitmapSource>();
		static BitmapSource TryGetImage(ImageSourceInfo img, Size size)
		{
			if (img is null)
				return null;

			var key = new ImageKey(img.Uri, size);
			
			BitmapSource image;
			
			if (imageCache.TryGetValue(key, out var bmp))
				return bmp;

			image = TryLoadImage(img, size);
			
			if (image is null)
				return null;

			imageCache[key] = image;
			return image;
		}

		struct ImageKey
		{
			private string uriString;
			private Size size;

			public ImageKey(string uriString, Size size)
			{
				this.uriString = uriString;
				this.size = size;
			}

			public override int GetHashCode()
			{
				return uriString.GetHashCode() ^ size.GetHashCode();
			}

			public override bool Equals(object obj)
			{
				if (obj == null) return false;
				var other = (ImageKey)obj;
				return size.Equals(other.size) && uriString.Equals(other.uriString);
			}

			public override string ToString()
			{
				return $"{uriString} ({size})";
			}
		}

		static BitmapSource TryLoadImage(ImageSourceInfo img, Size size)
		{
			try
			{
				var uri = new Uri(img.Uri, UriKind.RelativeOrAbsolute);
				var info = Application.GetResourceStream(uri);
				if (info.ContentType.Equals("application/xaml+xml", StringComparison.OrdinalIgnoreCase) || info.ContentType.Equals("application/baml+xml", StringComparison.OrdinalIgnoreCase))
				{
					var component = Application.LoadComponent(uri);
					if (component is FrameworkElement elem)
						return ResizeElement(elem, size);
					return null;
				}
				else if (info.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
				{
					var decoder = BitmapDecoder.Create(info.Stream, BitmapCreateOptions.None, BitmapCacheOption.OnDemand);
					if (decoder.Frames.Count == 0)
						return null;
					return ResizeImage(decoder.Frames[0], size);
				}
				else
					return null;
			}
			catch
			{
				return null;
			}
		}

		static BitmapSource ResizeElement(FrameworkElement elem, Size physicalSize)
		{
			elem.Width = physicalSize.Width;
			elem.Height = physicalSize.Height;
			elem.Measure(physicalSize);
			elem.Arrange(new Rect(physicalSize));
			var dv = new DrawingVisual();
			using (var dc = dv.RenderOpen())
			{
				var brush = new VisualBrush(elem) { Stretch = Stretch.Uniform };
				dc.DrawRectangle(brush, null, new Rect(physicalSize));
			}
			Debug.Assert((int)physicalSize.Width == physicalSize.Width);
			Debug.Assert((int)physicalSize.Height == physicalSize.Height);
			var renderBmp = new RenderTargetBitmap((int)physicalSize.Width, (int)physicalSize.Height, 96, 96, PixelFormats.Pbgra32);
			renderBmp.Render(dv);
			return new FormatConvertedBitmap(renderBmp, PixelFormats.Bgra32, null, 0);
		}

		static BitmapSource ResizeImage(BitmapSource bitmapImage, Size physicalSize)
		{
			if (bitmapImage.PixelWidth == physicalSize.Width && bitmapImage.PixelHeight == physicalSize.Height)
				return bitmapImage;
			var image = new Image { Source = bitmapImage };
			RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
			return ResizeElement(image, physicalSize);
		}

		static CrispImageConverter()
		{
			InitResources();
		}

		class ImageSourceInfo
		{
			/// <summary>
			/// Any size
			/// </summary>
			public static readonly Size AnySize = new Size(0, 0);

			/// <summary>
			/// URI of image
			/// </summary>
			public string Uri { get; set; }

			/// <summary>
			/// Size of image in pixels or <see cref="AnySize"/>
			/// </summary>
			public Size Size { get; set; }
		}

		static Dictionary<string, ImageSourceInfo[]> imageResources;
		static void InitResources()
		{
			var asm = typeof(CrispImageConverter).Assembly;
			string resName = asm.GetName().Name + ".g.resources";
			using (var stream = asm.GetManifestResourceStream(resName))
			{
				if (stream == null) return; // design time
				using (var reader = new System.Resources.ResourceReader(stream))
				{
					imageResources = reader.Cast<DictionaryEntry>()
						.Select(x => (string)x.Key)
						.Where(key => key.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
						.Select(uri => new
						{
							Source = new ImageSourceInfo()
							{
								Uri = "/Swiddler;component/" + FixXamlExtension(uri),
								Size = GetImageSize(uri, out var nameKey)
							},
							Key = nameKey,
						})
						.ToLookup(x => x.Key, x => x.Source)
						.ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);
				}
			}

		}

		static string FixXamlExtension(string uri)
		{
			if (uri.EndsWith(".baml"))
				return uri.Substring(0, uri.Length - 4) + "xaml";
			return uri;
		}

		static readonly Regex sizeRegex = new Regex(@"(.+)_(\d+)x$");
		static Size GetImageSize(string name, out string nameKey)
		{
			name = name.Split('/').Last();
			nameKey = name.Substring(0, name.LastIndexOf('.'));

			var match = sizeRegex.Match(nameKey);
			if (!match.Success) return ImageSourceInfo.AnySize;
			
			nameKey = match.Groups[1].Value;
			int.TryParse(match.Groups[2].Value, out int sz);
			return new Size(sz, sz);
		}

	}
}
