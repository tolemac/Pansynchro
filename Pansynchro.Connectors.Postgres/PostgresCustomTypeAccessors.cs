using System.IO;
using System.Runtime.CompilerServices;

using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

using Pansynchro.Core.CustomTypes;
using Pansynchro.Core.DataDict;

namespace Pansynchro.Connectors.Postgres
{
	internal static class PostgresCustomTypeAccessors
	{
		private abstract class GeoTypeAccessorBase : ICustomTypeAccessor
		{
			private readonly WKTReader _wktReader = new();
			private readonly WKTWriter _wktWriter = new();

			public abstract TypeTag Type { get; }
			public string Provider => PostgresConnector.ProviderName;

			public object ToCanonical(object sqlValue)
			{
				if (sqlValue is Geometry geometry) {
					var srid = geometry.SRID > 0 ? (int?)geometry.SRID : null;
					return new CanonicalGeo(_wktWriter.Write(geometry), srid);
				}
				return sqlValue;
			}

			public object FromCanonical(object canonicalValue)
			{
				if (canonicalValue is Geometry geometry) {
					return geometry;
				}

				var geo = canonicalValue switch {
					CanonicalGeo parsedGeo => parsedGeo,
					string text => CanonicalGeo.Parse(text),
					_ => CanonicalGeo.Parse(canonicalValue.ToString())
				};

				var parsed = _wktReader.Read(geo.Wkt);
				if (geo.Srid.HasValue) {
					parsed.SRID = geo.Srid.Value;
				}
				return parsed;
			}
		}

		private class GeometryTypeAccessor : GeoTypeAccessorBase
		{
			public override TypeTag Type => TypeTag.Geometry;
		}

		private class GeographyTypeAccessor : GeoTypeAccessorBase
		{
			public override TypeTag Type => TypeTag.Geography;
		}

		[ModuleInitializer]
		public static void Register()
		{
			CustomTypeRegistry.RegisterTypeAccessor(new GeometryTypeAccessor());
			CustomTypeRegistry.RegisterTypeAccessor(new GeographyTypeAccessor());
		}
	}
}
