using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

using Pansynchro.Core;
using Pansynchro.Core.CustomTypes;
using Pansynchro.Core.DataDict;
using Pansynchro.Core.Errors;
using Pansynchro.Core.EventsSystem;

namespace Pansynchro.Connectors.TextFile.WholeFile
{
	public class TextFileWriter : IWriter, ISinkConnector
	{
		public string Provider => TextFileConnector.ProviderName;

		private IDataSink? _sink;

		public async Task Sync(IAsyncEnumerable<DataStream> streams, DataDictionary dest)
		{
			if (_sink == null) {
				throw new DataException("Must call SetDataSink before calling Sync");
			}
			EventLog.Instance.AddStartSyncEvent();
			await foreach (var (name, settings, stream) in streams) {
				EventLog.Instance.AddStartSyncStreamEvent(name);
				IDataReader outReader = stream;
				try {
					var streamDef = dest.GetStream(name.ToString());
					outReader = CustomTypeAccessorTransformations.ApplyForWrite(new DataStream(name, settings, stream), streamDef, Provider).Reader;
					using var writer = await _sink.WriteText(name.ToString());
					while (outReader.Read()) {
						writer.Write(outReader.GetString(outReader.GetOrdinal("Value")));
					}
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

		public void SetDataSink(IDataSink sink) => _sink = sink;

		public void Dispose()
		{
			(_sink as IDisposable)?.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
