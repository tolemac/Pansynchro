using System;
using System.Runtime.CompilerServices;
using System.Text;

using Pansynchro.Core.CustomTypes;
using Pansynchro.Core.DataDict;

namespace Pansynchro.Connectors.TextFile
{
	internal static class TextFileCustomTypeAccessors
	{
		private class GeometryAccessor(string provider) : ICustomTypeAccessor
		{
			public TypeTag Type => TypeTag.Geometry;
			public string Provider => provider;

			public object ToCanonical(object providerValue)
			{
				if (providerValue is CanonicalGeo geo) {
					return geo;
				}

				var text = providerValue switch {
					byte[] bytes => Encoding.UTF8.GetString(bytes),
					_ => providerValue.ToString() ?? string.Empty
				};

				return CanonicalGeo.Parse(text);
			}

			public object FromCanonical(object canonicalValue)
			{
				if (canonicalValue is CanonicalGeo geo) {
					return geo.ToText();
				}

				return canonicalValue.ToString() ?? string.Empty;
			}
		}

		private class GeographyAccessor(string provider) : ICustomTypeAccessor
		{
			public TypeTag Type => TypeTag.Geography;
			public string Provider => provider;

			public object ToCanonical(object providerValue)
			{
				if (providerValue is CanonicalGeo geo) {
					return geo;
				}

				var text = providerValue switch {
					byte[] bytes => Encoding.UTF8.GetString(bytes),
					_ => providerValue.ToString() ?? string.Empty
				};

				return CanonicalGeo.Parse(text);
			}

			public object FromCanonical(object canonicalValue)
			{
				if (canonicalValue is CanonicalGeo geo) {
					return geo.ToText();
				}

				return canonicalValue.ToString() ?? string.Empty;
			}
		}

		private static void RegisterProvider(string provider)
		{
			CustomTypeRegistry.RegisterTypeAccessor(new GeometryAccessor(provider));
			CustomTypeRegistry.RegisterTypeAccessor(new GeographyAccessor(provider));
		}

		[ModuleInitializer]
		public static void Register()
		{
			RegisterProvider(CSV.CsvConnector.ProviderName);
			RegisterProvider(HTML.HtmlConnector.ProviderName);
			RegisterProvider(Lines.TextLinesConnector.ProviderName);
			RegisterProvider(WholeFile.TextFileConnector.ProviderName);
		}
	}
}