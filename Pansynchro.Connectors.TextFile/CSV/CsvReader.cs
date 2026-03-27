using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Pansynchro.Core;
using Pansynchro.Core.CustomTypes;
using Pansynchro.Core.DataDict;
using Pansynchro.Core.Readers;

namespace Pansynchro.Connectors.TextFile.CSV
{
	public class CsvReader : IReader, ISourcedConnector, IRandomStreamReader
	{
		public string Provider => CsvConnector.ProviderName;

		private readonly string _conf;
		private IDataSource? _source;

		public CsvReader(string configuration)
		{
			_conf = configuration;
		}

		public IAsyncEnumerable<DataStream> ReadFrom(DataDictionary source)
		{
			if (_source == null) {
				throw new DataException("Must call SetDataSource before calling ReadFrom");
			}
			return DataStream.CombineStreamsByName(Impl());

			async IAsyncEnumerable<DataStream> Impl()
			{
				await foreach (var (name, reader) in _source.GetTextAsync()) {
					var streamName = StreamDescription.Parse(name);
					var data = new DataStream(streamName, StreamSettings.None, CreateReader(reader));
					try {
						if (source.HasStream(streamName.ToString())) {
							var stream = source.GetStream(streamName.ToString());
							yield return CustomTypeAccessorTransformations.ApplyForRead(data, stream, Provider);
						} else {
							yield return data;
						}
					} finally {
						data.Reader.Dispose();
					}
				}
			}
		}

		public Task<DataStream> ReadStream(DataDictionary source, string name)
		{
			if (_source == null) {
				throw new DataException("Must call SetDataSource before calling ReadStream");
			}
			if (!source.HasStream(name)) {
				throw new DataException($"No stream named '{name}' is defined in the data dictionary.");
			}
			var stream = source.GetStream(name);
			var readers = _source.GetTextAsync(name).Select(r => CreateReader(r));
			var data = new DataStream(stream.Name, StreamSettings.None, new GroupingReader(readers));
			return Task.FromResult(CustomTypeAccessorTransformations.ApplyForRead(data, stream, Provider));
		}

		private CsvDataReader CreateReader(TextReader reader)
		{
			var configurator = new CsvConfigurator(_conf);
			return new CsvDataReader(
				configurator.Pipelined != 0 ? new PipelinedCsvArrayProducer(reader, configurator) : new CsvArrayProducer(reader, configurator)
			);
		}

		public void SetDataSource(IDataSource source)
		{
			_source = source;
		}

		async Task<Exception?> IReader.TestConnection()
		{
			if (_source == null) {
				return new Exception("No data source has been set.");
			}
			try {
				await foreach (var (name, stream) in _source.GetDataAsync()) {
					break;
				}
			} catch (Exception e) {
				return e;
			}
			return null;
		}

		public void Dispose()
		{
			GC.SuppressFinalize(this);
		}
	}
}
