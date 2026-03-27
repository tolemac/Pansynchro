using System.Data.Common;
using System.Runtime.CompilerServices;

using Npgsql;
using Npgsql.NetTopologySuite;

using Pansynchro.Core;
using Pansynchro.Core.Connectors;
using Pansynchro.Core.DataDict;

namespace Pansynchro.Connectors.Postgres
{
	public class PostgresConnector : ConnectorCore
	{
		public static string ProviderName => "Postgres";
		public override string Name => ProviderName;

		public override Capabilities Capabilities => Capabilities.ALL;

		public override NameStrategyType Strategy => NameStrategyType.LowerCase;

		public override ISchemaAnalyzer GetAnalyzer(string config) => new PostgresSchemaAnalyzer(config);

		public override DbConnectionStringBuilder GetConfig() => new NpgsqlConnectionStringBuilder();

		public override IReader GetReader(string config) => new PostgresReader(config);

		public override IWriter GetWriter(string config) => new PostgresWriter(config);

		[ModuleInitializer]
		public static void Register()
		{
			NpgsqlConnection.GlobalTypeMapper.UseNetTopologySuite();
			ConnectorRegistry.RegisterConnector(new PostgresConnector());
		}
	}
}
