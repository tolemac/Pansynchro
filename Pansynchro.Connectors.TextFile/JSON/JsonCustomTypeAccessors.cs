using System;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

using Pansynchro.Core.CustomTypes;
using Pansynchro.Core.DataDict;

namespace Pansynchro.Connectors.TextFile.JSON
{
	internal static class JsonCustomTypeAccessors
	{
		private class GeometryTypeAccessor : ICustomTypeAccessor
		{
			public TypeTag Type => TypeTag.Geometry;
			public string Provider => JsonConnector.ProviderName;
			public object ToCanonical(object sourceValue) => JsonCustomTypeAccessors.ToCanonical(sourceValue);
			public object FromCanonical(object canonicalValue) => JsonCustomTypeAccessors.FromCanonical(canonicalValue, null);
		}

		private class GeographyTypeAccessor : ICustomTypeAccessor
		{
			public TypeTag Type => TypeTag.Geography;
			public string Provider => JsonConnector.ProviderName;
			public object ToCanonical(object sourceValue) => JsonCustomTypeAccessors.ToCanonical(sourceValue);
			public object FromCanonical(object canonicalValue) => JsonCustomTypeAccessors.FromCanonical(canonicalValue, null);
		}

		private static object ToCanonical(object sourceValue)
		{
			if (sourceValue is CanonicalGeo) {
				return sourceValue;
			}

			if (sourceValue is JsonObject obj) {
				var wkt = obj["wkt"]?.ToString() ?? obj["WKT"]?.ToString();
				var sridNode = obj["srid"] ?? obj["SRID"];
				if (!string.IsNullOrWhiteSpace(wkt)) {
					int? srid = null;
					if (sridNode != null && int.TryParse(sridNode.ToString(), out var parsed)) {
						srid = parsed;
					}
					return new CanonicalGeo(wkt, srid);
				}
			}

			var text = sourceValue switch {
				JsonValue jv => jv.TryGetValue<string>(out var s) ? s : jv.ToString(),
				JsonNode jn => jn.ToString(),
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
