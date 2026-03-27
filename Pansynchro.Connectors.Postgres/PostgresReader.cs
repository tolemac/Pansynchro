using System.Data.Common;

using Npgsql;

using Pansynchro.SQL;

namespace Pansynchro.Connectors.Postgres
{
	public class PostgresReader : SqlDbReader
	{
		public override string Provider => PostgresConnector.ProviderName;

		public PostgresReader(string connectionString) : base(connectionString)
		{ }

		protected override DbConnection CreateConnection(string connectionString)
			=> new NpgsqlConnection(connectionString);

		protected override ISqlFormatter SqlFormatter => PostgresFormatter.Instance;
	}
}
