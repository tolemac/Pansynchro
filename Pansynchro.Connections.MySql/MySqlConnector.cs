using System.Data.Common;
using System.Runtime.CompilerServices;

using MySqlConnector;

using Pansynchro.Core;
using Pansynchro.Core.Connectors;
using Pansynchro.Core.DataDict;

namespace Pansynchro.Connectors.MySQL
{
	public class MySqlConnector : ConnectorCore
	{
		public static string ProviderName => "MySql";
		public override string Name => ProviderName;

		public override Capabilities Capabilities => Capabilities.ALL;

		public override NameStrategyType Strategy => NameStrategyType.NameOnlyLowerCase;

		public override ISchemaAnalyzer GetAnalyzer(string config) => new MySqlSchemaAnalyzer(config);

		public override DbConnectionStringBuilder GetConfig() => new MySqlConnectionStringBuilder();

		public override IReader GetReader(string config) => new MySqlReader(config);

		public override IWriter GetWriter(string config) => new MySqlWriter(config);

		[ModuleInitializer]
		public static void Register()
		{
			ConnectorRegistry.RegisterConnector(new MySqlConnector());
		}
	}
}
