using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using MySqlConnector;

using Pansynchro.Core;
using Pansynchro.Core.CustomTypes;
using Pansynchro.Core.DataDict;
using Pansynchro.Core.Errors;
using Pansynchro.Core.EventsSystem;

namespace Pansynchro.Connectors.MySQL
{
	public class MySqlWriter : IWriter
	{
		public string Provider => MySqlConnector.ProviderName;

		private readonly MySqlConnection _conn;

		public MySqlWriter(string connectionString)
		{
			_conn = new(connectionString);
			_conn.InfoMessage += Conn_InfoMessage;
		}

		private void Conn_InfoMessage(object sender, MySqlInfoMessageEventArgs args)
		{
			foreach (var err in args.Errors) {
				EventLog.Instance.AddErrorEvent(null, null, $"{err.Level}: {err.Message}");
			}
		}

		const int BATCH_SIZE = 100_000;

		public async Task Sync(IAsyncEnumerable<DataStream> streams, DataDictionary dest)
		{
			EventLog.Instance.AddStartSyncEvent();
			await foreach (var (name, averageSize, reader) in streams) {
				EventLog.Instance.AddStartSyncStreamEvent(name);
				IDataReader outReader = reader;
				try {
					var streamDef = dest.GetStream(name, NameStrategy.Get(NameStrategyType.NameOnlyLowerCase));
					outReader = CustomTypeAccessorTransformations.ApplyForWrite(new DataStream(name, averageSize, reader), streamDef, Provider).Reader;
					ulong progress = 0;
					var copy = new MySqlBulkCopy(_conn) {
						DestinationTableName = $"`{name}`",
						NotifyAfter = BATCH_SIZE
					};
					copy.MySqlRowsCopied += (s, e) => progress = (ulong)e.RowsCopied;
					copy.ColumnMappings.AddRange(GetColumnMapping(outReader));
					var stopwatch = new Stopwatch();
					stopwatch.Start();
					copy.WriteToServer(outReader);
					stopwatch.Stop();
				} catch (Exception ex) {
					EventLog.Instance.AddErrorEvent(ex, name);
					if (!ErrorManager.ContinueOnError)
						throw;
				} finally {
					outReader.Dispose();
				}
				EventLog.Instance.AddEndSyncStreamEvent(name);
			}
			EventLog.Instance.AddEndSyncEvent();
		}

		private static IEnumerable<MySqlBulkCopyColumnMapping> GetColumnMapping(IDataReader reader) =>
			Enumerable.Range(0, reader.FieldCount)
				.Select(i => new MySqlBulkCopyColumnMapping(i, reader.GetName(i)));

		public void Dispose()
		{
			_conn.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
