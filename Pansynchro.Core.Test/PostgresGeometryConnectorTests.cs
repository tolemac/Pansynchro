using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

using NUnit.Framework;

using Pansynchro.Connectors.Postgres;
using Pansynchro.Core.CustomTypes;
using Pansynchro.Core.DataDict;

namespace Pansynchro.Core.Test;

[TestFixture]
public class PostgresGeometryConnectorTests
{
	[OneTimeSetUp]
	public void LoadConnectorAssembly()
	{
		_ = PostgresFormatter.Instance;
	}

	[Test]
	public void PostgresGeometry_RoundTrip_ThroughRegistry_PreservesPointAndSrid()
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, "Postgres")!;
		var input = new CanonicalGeo("POINT (30 10)", 3857);

		var providerValue = accessor.FromCanonical(input);
		Assert.That(providerValue, Is.InstanceOf<Geometry>());

		var output = (CanonicalGeo)accessor.ToCanonical(providerValue);

		Assert.That(output.Srid, Is.EqualTo(3857));
		AssertSamePoint(input.Wkt, output.Wkt);
	}

	[Test]
	public void PostgresGeography_FromCanonicalString_UsesCoreCanonicalFormat()
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geography, "Postgres")!;

		var providerValue = accessor.FromCanonical("SRID=4326;POINT (10 30)");
		var output = (CanonicalGeo)accessor.ToCanonical(providerValue);

		Assert.That(output.Srid, Is.EqualTo(4326));
		AssertSamePoint("POINT (10 30)", output.Wkt);
	}

	[Test]
	public void Postgres_ToCanonical_WhenValueIsNtsGeometryWithNoSrid_SridIsNull()
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, "Postgres")!;
		var geomSridZero = new WKTReader().Read("POINT (9 10)");
		geomSridZero.SRID = 0;
		var geomSridMinusOne = new WKTReader().Read("POINT (9 10)");
		geomSridMinusOne.SRID = -1;

		var resultZero = (CanonicalGeo)accessor.ToCanonical(geomSridZero);
		var resultMinusOne = (CanonicalGeo)accessor.ToCanonical(geomSridMinusOne);

		Assert.That(resultZero.Srid, Is.Null, "SRID=0 should map to null");
		Assert.That(resultMinusOne.Srid, Is.Null, "SRID=-1 (NTS unset) should map to null");
		AssertSamePoint("POINT (9 10)", resultZero.Wkt);
	}

	[Test]
	public void Postgres_FromCanonical_WhenValueIsAlreadyNtsGeometry_ReturnsSameObject()
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, "Postgres")!;
		var geom = new WKTReader().Read("POINT (1 2)");

		var result = accessor.FromCanonical(geom);

		Assert.That(result, Is.SameAs(geom));
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
