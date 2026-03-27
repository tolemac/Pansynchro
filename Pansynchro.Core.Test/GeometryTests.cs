using NUnit.Framework;

using Pansynchro.Core.CustomTypes;

namespace Pansynchro.Core.Test;

[TestFixture]
public class GeometryTests
{
	[Test]
	public void Parse_WithSridPrefix_ExtractsSridAndWkt()
	{
		var parsed = CanonicalGeo.Parse("SRID=4326;POINT (30 10)");

		Assert.That(parsed.Srid, Is.EqualTo(4326));
		Assert.That(parsed.Wkt, Is.EqualTo("POINT (30 10)"));
	}

	[Test]
	public void Parse_WithoutSrid_UsesProvidedDefaultSrid()
	{
		var parsed = CanonicalGeo.Parse("POINT (30 10)", 3857);

		Assert.That(parsed.Srid, Is.EqualTo(3857));
		Assert.That(parsed.Wkt, Is.EqualTo("POINT (30 10)"));
	}

	[Test]
	public void ToText_WithSrid_PreservesCanonicalPrefix()
	{
		var geo = new CanonicalGeo("POINT (30 10)", 4326);

		var text = geo.ToText();

		Assert.That(text, Is.EqualTo("SRID=4326;POINT (30 10)"));
	}

	[Test]
	public void ToText_UsesDefaultSrid_WhenInstanceHasNoSrid()
	{
		var geo = new CanonicalGeo("POINT (30 10)", null);

		var text = geo.ToText(4326);

		Assert.That(text, Is.EqualTo("SRID=4326;POINT (30 10)"));
	}

	[Test]
	public void WithDefaultSrid_DoesNotOverrideExistingSrid()
	{
		var geo = new CanonicalGeo("POINT (30 10)", 3857);

		var result = geo.WithDefaultSrid(CanonicalGeo.Wgs84Srid);

		Assert.That(result.Srid, Is.EqualTo(3857));
		Assert.That(result.Wkt, Is.EqualTo("POINT (30 10)"));
	}
}
