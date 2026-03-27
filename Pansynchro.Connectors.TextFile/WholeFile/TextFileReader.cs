using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Pansynchro.Core;
using Pansynchro.Core.CustomTypes;
using Pansynchro.Core.DataDict;
using Pansynchro.Core.Readers;

namespace Pansynchro.Connectors.TextFile.WholeFile
{
	class TextFileReader : IReader, ISourcedConnector, IRandomStreamReader
	{
		public string Provider => TextFileConnector.ProviderName;

		private IDataSource? _source;
		private readonly string _config;

		public TextFileReader(string config)
		{
			_config = config;
		}

		IAsyncEnumerable<DataStream> IReader.ReadFrom(DataDictionary source)
		{
			if (_source == null) {
				throw new DataException("Must call SetDataSource before calling ReadFrom");
			}
			return DataStream.CombineStreamsByName(Impl());

			async IAsyncEnumerable<DataStream> Impl()
			{
				await foreach (var (name, reader) in _source.GetTextAsync()) {
					var streamName = new StreamDescription(null, name);
					var data = new DataStream(streamName, StreamSettings.None, new WholeFileReader(name, await reader.ReadToEndAsync()));
					try {
						if (source.HasStream(name)) {
							yield return CustomTypeAccessorTransformations.ApplyForRead(data, source.GetStream(name), Provider);
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
			var values = _source.GetTextAsync(name)
				.Select(async (System.IO.TextReader r, CancellationToken t) => new WholeFileReader(name, await r.ReadToEndAsync(t)));
			var result = new GroupingReader(values);
			var stream = source.GetStream(name);
			var data = new DataStream(StreamDescription.Parse(name), StreamSettings.None, result);
			return Task.FromResult(CustomTypeAccessorTransformations.ApplyForRead(data, stream, Provider));
		}

		void ISourcedConnector.SetDataSource(IDataSource source) => _source = source;

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

		void IDisposable.Dispose()
		{
			GC.SuppressFinalize(this);
		}
	}

	internal class WholeFileReader : ArrayReader
	{
		private static readonly string[] NAMES = new[] { "Name", "Value" };

		public WholeFileReader(string name, string value)
		{
			_buffer = new object[2] { name, value };
		}

		private bool _readOnce;

		public override int RecordsAffected => 1;

		public override bool Read()
		{
			var result = !_readOnce;
			_readOnce = true;
			return result;
		}

		public override string GetName(int i) => NAMES[i];

		public override int GetOrdinal(string name) => Array.IndexOf(NAMES, name);

		public override void Dispose()
		{ }
	}
}
