using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

using MySqlConnector;

using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

using Pansynchro.Core.CustomTypes;
using Pansynchro.Core.DataDict;

namespace Pansynchro.Connectors.MySQL
{
	internal static class MySqlCustomTypeAccessors
	{
		private class GeometryAccessor : ICustomTypeAccessor
		{
			private static readonly WKTReader _wktReader = new();
			private static readonly WKTWriter _wktWriter = new();
			private static readonly WKBReader _wkbReader = new();
			private static readonly WKBWriter _wkbWriter = new();

			public virtual TypeTag Type => TypeTag.Geometry;
			public string Provider => global::Pansynchro.Connectors.MySQL.MySqlConnector.ProviderName;

			public virtual object ToCanonical(object providerValue)
			{
				if (providerValue is CanonicalGeo) {
					return providerValue;
				}

				if (providerValue is Geometry geometry) {
					var srid = geometry.SRID == 0 ? null : (int?)geometry.SRID;
					return new CanonicalGeo(_wktWriter.Write(geometry), srid);
				}

				if (providerValue is MySqlGeometry mySqlGeometry) {
					return ParseString(mySqlGeometry.ToString() ?? string.Empty, null);
				}

				if (providerValue is byte[] raw) {
					return ParseBytes(raw, null);
				}

				return ParseString(providerValue.ToString() ?? string.Empty, null);
			}

			public virtual object FromCanonical(object canonicalValue)
			{
				if (canonicalValue is byte[]) {
					return canonicalValue;
				}

				if (canonicalValue is Geometry geometry) {
					return SerializeGeometry(geometry, geometry.SRID == 0 ? null : (int?)geometry.SRID);
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
				return SerializeGeometry(parsed, geo.Srid);
			}

			private static object ParseBytes(byte[] raw, int? defaultSrid)
			{
				if (raw.Length < 5) {
					return new CanonicalGeo(Convert.ToBase64String(raw), defaultSrid);
				}

				try {
					var srid = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(0, 4));
					var geom = _wkbReader.Read(raw.AsSpan(4).ToArray());
					if (srid > 0) {
						geom.SRID = srid;
					}
					return new CanonicalGeo(_wktWriter.Write(geom), srid > 0 ? srid : defaultSrid);
				} catch {
					try {
						var geom = _wkbReader.Read(raw);
						var srid = geom.SRID == 0 ? defaultSrid : (int?)geom.SRID;
						return new CanonicalGeo(_wktWriter.Write(geom), srid);
					} catch {
						return new CanonicalGeo(Convert.ToBase64String(raw), defaultSrid);
					}
				}
			}

			private static object ParseString(string text, int? defaultSrid)
			{
				return CanonicalGeo.Parse(text, defaultSrid);
			}

			private static byte[] SerializeGeometry(Geometry geometry, int? srid)
			{
				var wkb = _wkbWriter.Write(geometry);
				var result = new byte[wkb.Length + 4];
				BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(0, 4), srid ?? 0);
				Buffer.BlockCopy(wkb, 0, result, 4, wkb.Length);
				return result;
			}
		}

		private class GeographyAccessor : GeometryAccessor
		{
			public override TypeTag Type => TypeTag.Geography;
		}

		[ModuleInitializer]
		public static void Register()
		{
			CustomTypeRegistry.RegisterTypeAccessor(new GeometryAccessor());
			CustomTypeRegistry.RegisterTypeAccessor(new GeographyAccessor());
		}
	}
}