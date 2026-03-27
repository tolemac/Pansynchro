using System;
using System.Runtime.CompilerServices;
using System.Text;

using Pansynchro.Core.CustomTypes;
using Pansynchro.Core.DataDict;

namespace Pansynchro.Connectors.Avro
{
	internal static class AvroCustomTypeAccessors
	{
		private class GeometryAccessor : ICustomTypeAccessor
		{
			public TypeTag Type => TypeTag.Geometry;
			public string Provider => AvroConnector.ProviderName;

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

		private class GeographyAccessor : ICustomTypeAccessor
		{
			public TypeTag Type => TypeTag.Geography;
			public string Provider => AvroConnector.ProviderName;

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

		[ModuleInitializer]
		public static void Register()
		{
			CustomTypeRegistry.RegisterTypeAccessor(new GeometryAccessor());
			CustomTypeRegistry.RegisterTypeAccessor(new GeographyAccessor());
		}
	}
}