using System;
using System.Runtime.CompilerServices;
using System.Text;

using Pansynchro.Core.CustomTypes;
using Pansynchro.Core.DataDict;

namespace Pansynchro.Connectors.Parquet
{
	internal static class ParquetCustomTypeAccessors
	{
		private class GeometryTypeAccessor : ICustomTypeAccessor
		{
			public TypeTag Type => TypeTag.Geometry;
			public string Provider => ParquetConnector.ProviderName;
			public object ToCanonical(object sourceValue) => ParquetCustomTypeAccessors.ToCanonical(sourceValue);
			public object FromCanonical(object canonicalValue) => ParquetCustomTypeAccessors.FromCanonical(canonicalValue, null);
		}

		private class GeographyTypeAccessor : ICustomTypeAccessor
		{
			public TypeTag Type => TypeTag.Geography;
			public string Provider => ParquetConnector.ProviderName;
			public object ToCanonical(object sourceValue) => ParquetCustomTypeAccessors.ToCanonical(sourceValue);
			public object FromCanonical(object canonicalValue) => ParquetCustomTypeAccessors.FromCanonical(canonicalValue, null);
		}

		private static object ToCanonical(object sourceValue)
		{
			if (sourceValue is CanonicalGeo) {
				return sourceValue;
			}

			var text = sourceValue switch {
				byte[] b => Encoding.UTF8.GetString(b),
				_ => sourceValue.ToString() ?? string.Empty
			};

			return CanonicalGeo.Parse(text);
		}

		private static object FromCanonical(object canonicalValue, int? defaultSrid)
		{
			if (canonicalValue is CanonicalGeo geo) {
				return geo.ToText(defaultSrid);
			}
			return canonicalValue;
		}

		[ModuleInitializer]
		public static void Register()
		{
			CustomTypeRegistry.RegisterTypeAccessor(new GeometryTypeAccessor());
			CustomTypeRegistry.RegisterTypeAccessor(new GeographyTypeAccessor());
		}
	}
}
