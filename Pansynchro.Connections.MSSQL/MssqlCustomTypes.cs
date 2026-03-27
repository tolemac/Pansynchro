using System.IO;
using System.Runtime.CompilerServices;
using System.Data.SqlTypes;

using Microsoft.SqlServer.Types;

using Pansynchro.Core.CustomTypes;
using Pansynchro.Core.DataDict;

namespace Pansynchro.Connectors.MSSQL
{
	internal static class MssqlCustomTypes
	{
		private class HierarchySupport : IProtocolCustomType
		{
			public string Name => typeof(SqlHierarchyId).FullName!;

			public TypeTag Type => TypeTag.HierarchyID;

			public object ProtocolReader(BinaryReader r)
			{
				// this is horrifically inefficient, but SqlHierarchyId's broken serialization requires this as a workaround
				// https://github.com/dotMorten/Microsoft.SqlServer.Types/issues/64
				var result = new SqlHierarchyId();
				var len = r.Read7BitEncodedInt();
				using var ms = new MemoryStream(r.ReadBytes(len));
				using var br = new BinaryReader(ms);
				result.Read(br);
				return result;
			}

			public void ProtocolWriter(object o, BinaryWriter s)
			{
				// this is horrifically inefficient, but SqlHierarchyId's broken serialization requires this as a workaround
				// https://github.com/dotMorten/Microsoft.SqlServer.Types/issues/64
				var id = (SqlHierarchyId)o;
				using var ms = new MemoryStream();
				using var sw = new BinaryWriter(ms);
				id.Write(sw);
				s.Write7BitEncodedInt((int)ms.Length);
				s.Write(ms.GetBuffer(), 0, (int)ms.Length);
			}
		}

		private class GeometrySupport : IProtocolCustomType
		{
			public string Name => typeof(SqlGeometry).FullName!;

			public TypeTag Type => TypeTag.Geometry;

			public object ProtocolReader(BinaryReader r)
			{
				var result = new SqlGeometry();
				result.Read(r);
				return result;
			}

			public void ProtocolWriter(object o, BinaryWriter s)
			{
				var value = (SqlGeometry)o;
				value.Write(s);
			}
		}

		private class GeographySupport : IProtocolCustomType
		{
			public string Name => typeof(SqlGeography).FullName!;

			public TypeTag Type => TypeTag.Geography;

			public object ProtocolReader(BinaryReader r)
			{
				var result = new SqlGeography();
				result.Read(r);
				return result;
			}

			public void ProtocolWriter(object o, BinaryWriter s)
			{
				var value = (SqlGeography)o;
				value.Write(s);
			}
		}

		private class GeometryTypeAccessor : ICustomTypeAccessor
		{
			public TypeTag Type => TypeTag.Geometry;
			public string Provider => "MSSQL";

			public object ToCanonical(object sqlValue)
			{
				if (sqlValue is SqlGeometry geometry) {
					var srid = geometry.STSrid.IsNull ? null : (int?)geometry.STSrid.Value;
					return new CanonicalGeo(new string(geometry.STAsText().Value), srid);
				}
				return sqlValue;
			}

			public object FromCanonical(object canonicalValue)
			{
				var (wkt, srid) = canonicalValue switch {
					CanonicalGeo geo => (geo.Wkt, geo.Srid ?? 0),
					string text => (text, 0),
					_ => (canonicalValue.ToString() ?? string.Empty, 0)
				};
				return SqlGeometry.STGeomFromText(new SqlChars(wkt), srid);
			}
		}

		private class GeographyTypeAccessor : ICustomTypeAccessor
		{
			public TypeTag Type => TypeTag.Geography;
			public string Provider => "MSSQL";

			public object ToCanonical(object sqlValue)
			{
				if (sqlValue is SqlGeography geography) {
					var srid = geography.STSrid.IsNull ? null : (int?)geography.STSrid.Value;
					return new CanonicalGeo(new string(geography.STAsText().Value), srid);
				}
				return sqlValue;
			}

			public object FromCanonical(object canonicalValue)
			{
				var (wkt, srid) = canonicalValue switch {
					CanonicalGeo geo => (geo.Wkt, geo.Srid ?? CanonicalGeo.Wgs84Srid),
					string text => (text, CanonicalGeo.Wgs84Srid),
					_ => (canonicalValue.ToString() ?? string.Empty, CanonicalGeo.Wgs84Srid)
				};
				return SqlGeography.STGeomFromText(new SqlChars(wkt), srid);
			}
		}

		[ModuleInitializer]
		public static void Register()
		{
			CustomTypeRegistry.RegisterProtocolType(new HierarchySupport());
			CustomTypeRegistry.RegisterProtocolType(new GeometrySupport());
			CustomTypeRegistry.RegisterProtocolType(new GeographySupport());
			CustomTypeRegistry.RegisterTypeAccessor(new GeometryTypeAccessor());
			CustomTypeRegistry.RegisterTypeAccessor(new GeographyTypeAccessor());
		}
	}
}
