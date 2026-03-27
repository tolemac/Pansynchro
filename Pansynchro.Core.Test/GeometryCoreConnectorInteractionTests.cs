using NUnit.Framework;

using Pansynchro.Connectors.MSSQL;
using Pansynchro.Connectors.MySQL;
using Pansynchro.Connectors.Postgres;
using Pansynchro.Core.CustomTypes;
using Pansynchro.Core.DataDict;

namespace Pansynchro.Core.Test;

[TestFixture]
public class GeometryCoreConnectorInteractionTests
{
	[OneTimeSetUp]
	public void LoadConnectorAssemblies()
	{
		_ = MssqlFormatter.Instance;
		_ = MySqlFormatter.Instance;
		_ = PostgresFormatter.Instance;
	}

	private static IEnumerable<TestCaseData> ProviderGeometryTypes()
	{
		yield return new TestCaseData(TypeTag.Geometry, "MSSQL").SetName("MSSQL Geometry registered");
		yield return new TestCaseData(TypeTag.Geography, "MSSQL").SetName("MSSQL Geography registered");
		yield return new TestCaseData(TypeTag.Geometry, "MySql").SetName("MySQL Geometry registered");
		yield return new TestCaseData(TypeTag.Geography, "MySql").SetName("MySQL Geography registered");
		yield return new TestCaseData(TypeTag.Geometry, "Postgres").SetName("Postgres Geometry registered");
		yield return new TestCaseData(TypeTag.Geography, "Postgres").SetName("Postgres Geography registered");
	}

	[TestCaseSource(nameof(ProviderGeometryTypes))]
	public void Registry_ContainsGeometryAccessors_ForCoreAndConnectorsContract(TypeTag type, string provider)
	{
		var accessor = CustomTypeRegistry.GetTypeAccessor(type, provider);

		Assert.That(accessor, Is.Not.Null);
	}
}
