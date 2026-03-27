using System;
using System.Data.Common;
using System.Runtime.CompilerServices;

using Pansynchro.Core;
using Pansynchro.Core.Connectors;
using Pansynchro.Core.DataDict;

namespace Pansynchro.Connectors.TextFile.WholeFile
{
	public class TextFileConnector : ConnectorCore
	{
		public static string ProviderName => "Text File (whole)";
		public override string Name => ProviderName;

		public override Capabilities Capabilities => Capabilities.Reader | Capabilities.Writer | Capabilities.Analyzer | Capabilities.RandomAccessReader;

		public override NameStrategyType Strategy => NameStrategyType.NameOnly;

		public override ISchemaAnalyzer GetAnalyzer(string config) => new TextFileAnalyzer(config);

		public override DbConnectionStringBuilder GetConfig()
		{
			throw new NotImplementedException();
		}

		public override IReader GetReader(string config) => new TextFileReader(config);

		public override IWriter GetWriter(string config) => new TextFileWriter();

		[ModuleInitializer]
		public static void Run()
		{
			ConnectorRegistry.RegisterConnector(new TextFileConnector());
		}
	}
}
