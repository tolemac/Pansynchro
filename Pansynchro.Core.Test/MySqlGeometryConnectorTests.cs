using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

using NUnit.Framework;

using Pansynchro.Connectors.MySQL;
using Pansynchro.Core.CustomTypes;
using Pansynchro.Core.DataDict;

namespace Pansynchro.Core.Test;

[TestFixture]
public class MySqlGeometryConnectorTests
{
	[OneTimeSetUp]
	public void LoadConnectorAssembly()
	{
		_ = MySqlFormatter.Instance;
	}

	[Test]
	public void MySqlGeometry_RoundTrip_ThroughRegistry_PreservesPointAndSrid()
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, "MySql")!;
		var input = new CanonicalGeo("POINT (30 10)", 4326);

		var providerValue = accessor.FromCanonical(input);
		Assert.That(providerValue, Is.InstanceOf<byte[]>());

		var output = (CanonicalGeo)accessor.ToCanonical(providerValue);

		Assert.That(output.Srid, Is.EqualTo(4326));
		AssertSamePoint(input.Wkt, output.Wkt);
	}

	[Test]
	public void MySql_ToCanonical_WhenValueIsNtsGeometry_ReturnsCanonicalGeoWithSrid()
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, "MySql")!;
		var wktReader = new WKTReader();
		var geom = wktReader.Read("POINT (1 2)");
		geom.SRID = 4326;

		var result = (CanonicalGeo)accessor.ToCanonical(geom);

		Assert.That(result.Srid, Is.EqualTo(4326));
		AssertSamePoint("POINT (1 2)", result.Wkt);
	}

	[Test]
	public void MySql_ToCanonical_WhenValueIsAlreadyCanonicalGeo_ReturnsAsIs()
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, "MySql")!;
		var original = new CanonicalGeo("POINT (5 6)", 3857);

		var result = accessor.ToCanonical(original);

		Assert.That(result, Is.EqualTo(original));
	}

	[Test]
	public void MySql_ToCanonical_WhenValueIsWktString_ParsesCorrectly()
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, "MySql")!;

		var result = (CanonicalGeo)accessor.ToCanonical("POINT (7 8)");

		AssertSamePoint("POINT (7 8)", result.Wkt);
	}

	[Test]
	public void MySql_ToCanonical_WhenValueIsRawWkbWithSridZero_SridIsNull()
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, "MySql")!;
		var wkbWriter = new WKBWriter();
		var wktReader = new WKTReader();
		var geom = wktReader.Read("POINT (3 4)");
		var wkb = wkbWriter.Write(geom);
		var raw = new byte[4 + wkb.Length];
		Buffer.BlockCopy(wkb, 0, raw, 4, wkb.Length);

		var result = (CanonicalGeo)accessor.ToCanonical(raw);

		Assert.That(result.Srid, Is.Null);
		AssertSamePoint("POINT (3 4)", result.Wkt);
	}

	[Test]
	public void MySql_FromCanonical_WhenValueIsByteArray_ReturnsSameBytes()
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, "MySql")!;
		var raw = new byte[] { 1, 2, 3, 4, 5 };

		var result = accessor.FromCanonical(raw);

		Assert.That(result, Is.SameAs(raw));
	}

	private static void AssertSamePoint(string expectedWkt, string actualWkt)
	{
		var reader = new WKTReader();
		var expected = (Point)reader.Read(expectedWkt);
		var actual = (Point)reader.Read(actualWkt);

		Assert.That(actual.X, Is.EqualTo(expected.X).Within(0.000001));
		Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(0.000001));
	}
}
