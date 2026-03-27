using System.Text;
using System.Text.Json.Nodes;

using NUnit.Framework;

using Pansynchro.Connectors.Avro;
using Pansynchro.Connectors.Excel;
using Pansynchro.Connectors.Firebird;
using Pansynchro.Connectors.Parquet;
using Pansynchro.Connectors.Snowflake;
using Pansynchro.Connectors.Sqlite;
using Pansynchro.Connectors.TextFile.CSV;
using Pansynchro.Connectors.TextFile.JSON;
using Pansynchro.Connectors.TextFile.Lines;
using Pansynchro.Connectors.TextFile.WholeFile;
using Pansynchro.Core.CustomTypes;
using Pansynchro.Core.DataDict;

namespace Pansynchro.Core.Test;

[TestFixture]
public class OtherConnectorsGeometryTests
{
	[OneTimeSetUp]
	public void LoadConnectorAssemblies()
	{
		// Touching the ProviderName property of each connector class is sufficient to load
		// the assembly and trigger all [ModuleInitializer] attribute-decorated methods.
		_ = AvroConnector.ProviderName;
		_ = ExcelConnector.ProviderName;
		_ = FirebirdConnector.ProviderName;
		_ = SnowflakeConnector.ProviderName;
		_ = SqliteConnector.ProviderName;
		_ = ParquetConnector.ProviderName;
		_ = CsvConnector.ProviderName;
		_ = TextLinesConnector.ProviderName;
		_ = TextFileConnector.ProviderName;     // WholeFile
		_ = JsonConnector.ProviderName;
	}

	// =========================================================================
	// Registry tests — every connector must register Geometry AND Geography
	// =========================================================================

	private static IEnumerable<TestCaseData> AllNewProviderGeometryTypes()
	{
		var providers = new[]
		{
			"Avro", "Excel", "Firebird", "Snowflake", "Sqlite", "Parquet",
			"CSV", "HTML", "Text File (lines)", "Text File (whole)", "JSON"
		};

		foreach (var provider in providers)
		{
			yield return new TestCaseData(TypeTag.Geometry, provider).SetName($"{provider} Geometry registered");
			yield return new TestCaseData(TypeTag.Geography, provider).SetName($"{provider} Geography registered");
		}
	}

	[TestCaseSource(nameof(AllNewProviderGeometryTypes))]
	public void Registry_ContainsGeometryAccessors_ForFileConnectors(TypeTag type, string provider)
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(type, provider);

		Assert.That(accessor, Is.Not.Null);
	}

	// =========================================================================
	// ToCanonical / FromCanonical — text-based providers (common pattern)
	// All text connectors: ToCanonical parses a WKT string (or SRID prefix form).
	//                      FromCanonical serialises CanonicalGeo back to that text form.
	// =========================================================================

	private static IEnumerable<TestCaseData> AllTextProviders()
	{
		var providers = new[]
		{
			"Avro", "Excel", "Firebird", "Snowflake", "Sqlite", "Parquet",
			"CSV", "HTML", "Text File (lines)", "Text File (whole)", "JSON"
		};

		foreach (var p in providers)
			yield return new TestCaseData(p).SetName(p);
	}

	[TestCaseSource(nameof(AllTextProviders))]
	public void TextConnector_ToCanonical_PlainWktString_ParsesCorrectly(string provider)
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, provider)!;

		var result = (CanonicalGeo)accessor.ToCanonical("POINT (1 2)");

		Assert.That(result.Wkt, Is.EqualTo("POINT (1 2)"));
		Assert.That(result.Srid, Is.Null);
	}

	[TestCaseSource(nameof(AllTextProviders))]
	public void TextConnector_ToCanonical_WktStringWithSridPrefix_ParsesSridAndWkt(string provider)
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, provider)!;

		var result = (CanonicalGeo)accessor.ToCanonical("SRID=4326;POINT (1 2)");

		Assert.That(result.Wkt, Is.EqualTo("POINT (1 2)"));
		Assert.That(result.Srid, Is.EqualTo(4326));
	}

	[TestCaseSource(nameof(AllTextProviders))]
	public void TextConnector_ToCanonical_CanonicalGeoInput_PassesThroughUnchanged(string provider)
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, provider)!;
		var original = new CanonicalGeo("POINT (3 4)", 3857);

		var result = accessor.ToCanonical(original);

		Assert.That(result, Is.EqualTo(original));
	}

	[TestCaseSource(nameof(AllTextProviders))]
	public void TextConnector_FromCanonical_WithSrid_ReturnsCanonicalTextFormat(string provider)
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, provider)!;
		var geo = new CanonicalGeo("POINT (5 6)", 4326);

		var result = accessor.FromCanonical(geo);

		Assert.That(result, Is.EqualTo("SRID=4326;POINT (5 6)"));
	}

	[TestCaseSource(nameof(AllTextProviders))]
	public void TextConnector_FromCanonical_WithoutSrid_ReturnsWktOnly(string provider)
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, provider)!;
		var geo = new CanonicalGeo("POINT (5 6)", null);

		var result = accessor.FromCanonical(geo);

		Assert.That(result, Is.EqualTo("POINT (5 6)"));
	}

	// =========================================================================
	// byte[] UTF-8 branch — Avro, Parquet, and all TextFile connectors accept
	// a UTF-8 encoded byte array in addition to a plain string.
	// =========================================================================

	private static IEnumerable<TestCaseData> ByteCapableProviders()
	{
		var providers = new[]
		{
			"Avro", "Parquet", "CSV", "HTML", "Text File (lines)", "Text File (whole)"
		};

		foreach (var p in providers)
			yield return new TestCaseData(p).SetName(p);
	}

	[TestCaseSource(nameof(ByteCapableProviders))]
	public void ByteCapableConnector_ToCanonical_Utf8ByteArray_ParsesWkt(string provider)
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, provider)!;
		var bytes = Encoding.UTF8.GetBytes("POINT (7 8)");

		var result = (CanonicalGeo)accessor.ToCanonical(bytes);

		Assert.That(result.Wkt, Is.EqualTo("POINT (7 8)"));
		Assert.That(result.Srid, Is.Null);
	}

	[TestCaseSource(nameof(ByteCapableProviders))]
	public void ByteCapableConnector_ToCanonical_Utf8ByteArrayWithSridPrefix_ParsesSridAndWkt(string provider)
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, provider)!;
		var bytes = Encoding.UTF8.GetBytes("SRID=4326;POINT (7 8)");

		var result = (CanonicalGeo)accessor.ToCanonical(bytes);

		Assert.That(result.Wkt, Is.EqualTo("POINT (7 8)"));
		Assert.That(result.Srid, Is.EqualTo(4326));
	}

	// =========================================================================
	// JSON connector — unique behaviour: accepts JsonObject {wkt, srid} structure
	// in addition to plain strings and CanonicalGeo passthrough.
	// =========================================================================

	[Test]
	public void Json_ToCanonical_JsonObject_WithLowercaseWktAndSrid_ExtractsGeoCorrectly()
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, "JSON")!;
		var jsonObj = new JsonObject
		{
			["wkt"] = JsonValue.Create("POINT (10 20)"),
			["srid"] = JsonValue.Create(4326)
		};

		var result = (CanonicalGeo)accessor.ToCanonical(jsonObj);

		Assert.That(result.Wkt, Is.EqualTo("POINT (10 20)"));
		Assert.That(result.Srid, Is.EqualTo(4326));
	}

	[Test]
	public void Json_ToCanonical_JsonObject_WithUppercaseWktAndSrid_ExtractsGeoCorrectly()
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, "JSON")!;
		var jsonObj = new JsonObject
		{
			["WKT"] = JsonValue.Create("POINT (10 20)"),
			["SRID"] = JsonValue.Create(3857)
		};

		var result = (CanonicalGeo)accessor.ToCanonical(jsonObj);

		Assert.That(result.Wkt, Is.EqualTo("POINT (10 20)"));
		Assert.That(result.Srid, Is.EqualTo(3857));
	}

	[Test]
	public void Json_ToCanonical_JsonObject_WithWktOnly_SridIsNull()
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, "JSON")!;
		var jsonObj = new JsonObject
		{
			["WKT"] = JsonValue.Create("POINT (10 20)")
		};

		var result = (CanonicalGeo)accessor.ToCanonical(jsonObj);

		Assert.That(result.Wkt, Is.EqualTo("POINT (10 20)"));
		Assert.That(result.Srid, Is.Null);
	}

	[Test]
	public void Json_ToCanonical_JsonValue_ParsedAsWkt()
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, "JSON")!;
		var jsonValue = JsonValue.Create("POINT (11 22)");

		var result = (CanonicalGeo)accessor.ToCanonical(jsonValue!);

		Assert.That(result.Wkt, Is.EqualTo("POINT (11 22)"));
		Assert.That(result.Srid, Is.Null);
	}

	[Test]
	public void Json_ToCanonical_PlainStringWithSridPrefix_ParsesSridAndWkt()
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, "JSON")!;

		var result = (CanonicalGeo)accessor.ToCanonical("SRID=3857;POINT (11 22)");

		Assert.That(result.Wkt, Is.EqualTo("POINT (11 22)"));
		Assert.That(result.Srid, Is.EqualTo(3857));
	}

	[Test]
	public void Json_FromCanonical_CanonicalGeoWithSrid_ReturnsCanonicalTextFormat()
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, "JSON")!;
		var geo = new CanonicalGeo("POINT (1 2)", 4326);

		var result = accessor.FromCanonical(geo);

		Assert.That(result, Is.EqualTo("SRID=4326;POINT (1 2)"));
	}

	[Test]
	public void Json_FromCanonical_NonCanonicalValue_PassesThroughUnchanged()
	{
		// If the value is already in the provider's native format (e.g. a JsonObject
		// from a writer pipeline re-use), it must be returned unchanged.
		var accessor = CustomTypeRegistry.GetTypeAccessor(TypeTag.Geometry, "JSON")!;
		var jsonObj = new JsonObject { ["wkt"] = JsonValue.Create("POINT (1 2)") };

		var result = accessor.FromCanonical(jsonObj);

		Assert.That(result, Is.SameAs(jsonObj));
	}
}
