using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Parquet.Data;
using Parquet.Schema;
using PReader = Parquet.ParquetReader;
using SchemaType = Parquet.Schema.SchemaType;

using Pansynchro.Core;
using Pansynchro.Core.DataDict;
using Pansynchro.Core.Helpers;
using Pansynchro.Core.DataDict.TypeSystem;

namespace Pansynchro.Connectors.Parquet
{

	public class ParquetAnalyzer : ISchemaAnalyzer, ISourcedConnector
	{
		private IDataSource? _source;

		public ParquetAnalyzer(string config)
		{ }

		public async ValueTask<DataDictionary> AnalyzeAsync(string name)
		{
			if (_source == null) {
				throw new DataException("Must call SetDataSource before calling AnalyzeAsync");
			}
			string? lastName = null;
			var defs = new List<StreamDefinition>();
			await foreach (var (sName, stream) in _source.GetDataAsync()) {
				if (lastName != sName) {
					defs.Add(await AnalyzeFile(sName, stream));
					lastName = sName;
				} else {
					stream.Dispose();
				}
			}
			return new DataDictionary(name, defs.ToArray());
		}

		private static async Task<StreamDefinition> AnalyzeFile(string name, Stream stream)
		{
			using var lStream = StreamHelper.SeekableStream(stream);
			using var reader = await PReader.CreateAsync(lStream);
			var geoHints = ReadGeoHints(reader);
			var fields = reader.Schema.GetDataFields().Select(f => AnalyzeField(f, geoHints)).ToArray();
			return new StreamDefinition(new StreamDescription(null, name), fields, Array.Empty<string>());
		}

		private static FieldDefinition AnalyzeField(DataField field, IReadOnlyDictionary<string, TypeTag> geoHints)
		{
			TypeTag type;
			if (TryGetGeospatialType(field, geoHints, out var geoType)) {
				type = geoType;
			} else
			if (field.SchemaType == SchemaType.Data) {
				if (field.ClrType == typeof(bool)) type = TypeTag.Boolean;
				else if (field.ClrType == typeof(byte)) type = TypeTag.Byte;
				else if (field.ClrType == typeof(sbyte)) type = TypeTag.SByte;
				else if (field.ClrType == typeof(short)) type = TypeTag.Short;
				else if (field.ClrType == typeof(ushort)) type = TypeTag.UShort;
				else if (field.ClrType == typeof(int)) type = TypeTag.Int;
				else if (field.ClrType == typeof(uint)) type = TypeTag.UInt;
				else if (field.ClrType == typeof(long)) type = TypeTag.Long;
				else if (field.ClrType == typeof(ulong)) type = TypeTag.ULong;
				else if (field.ClrType == typeof(byte[])) type = TypeTag.Blob;
				else if (field.ClrType == typeof(string)) type = TypeTag.Ntext;
				else if (field.ClrType == typeof(float)) type = TypeTag.Single;
				else if (field.ClrType == typeof(double)) type = TypeTag.Double;
				else if (field.ClrType == typeof(decimal)) type = TypeTag.Decimal;
				else if (field.ClrType == typeof(DateTime)) type = TypeTag.DateTime;
				else if (field.ClrType == typeof(DateTimeOffset)) type = TypeTag.DateTimeTZ;
				else if (field.ClrType == typeof(TimeSpan)) type = TypeTag.Interval;
				else throw new NotSupportedException($"Parquet field '{field.Name}' is not of a supported type");
			} else throw new NotSupportedException($"Parquet field '{field.Name}' is of a {field.SchemaType} type, which is not currently supported");
			IFieldType fType = new BasicField(type, field.IsNullable, null, false);
			if (field.IsArray) {
				fType = new CollectionField(fType, CollectionType.Array, false);
			}
			return new FieldDefinition(field.Name, fType);
		}

		private static bool TryGetGeospatialType(DataField field, IReadOnlyDictionary<string, TypeTag> geoHints, out TypeTag type)
		{
			if (geoHints.TryGetValue(field.Name, out type)) {
				return true;
			}

			var logicalType = field.GetType().GetProperty("LogicalType")?.GetValue(field);
			if (logicalType == null) {
				type = TypeTag.Unstructured;
				return false;
			}

			var logicalName = logicalType.GetType().Name;
			if (logicalName.Contains("Geography", StringComparison.OrdinalIgnoreCase)) {
				type = TypeTag.Geography;
				return true;
			}
			if (logicalName.Contains("Geometry", StringComparison.OrdinalIgnoreCase)) {
				type = TypeTag.Geometry;
				return true;
			}

			var logicalText = logicalType.ToString() ?? string.Empty;
			if (logicalText.Contains("GEOGRAPHY", StringComparison.OrdinalIgnoreCase)) {
				type = TypeTag.Geography;
				return true;
			}
			if (logicalText.Contains("GEOMETRY", StringComparison.OrdinalIgnoreCase)) {
				type = TypeTag.Geometry;
				return true;
			}

			type = TypeTag.Unstructured;
			return false;
		}

		private static IReadOnlyDictionary<string, TypeTag> ReadGeoHints(PReader reader)
		{
			var result = new Dictionary<string, TypeTag>(StringComparer.OrdinalIgnoreCase);
			var metadataObj = reader.GetType().GetProperty("CustomMetadata")?.GetValue(reader);
			if (metadataObj is not IDictionary<string, string> metadata || !metadata.TryGetValue("geo", out var geoJson)) {
				return result;
			}

			try {
				using var doc = JsonDocument.Parse(geoJson);
				if (!doc.RootElement.TryGetProperty("columns", out var columns) || columns.ValueKind != JsonValueKind.Object) {
					return result;
				}

				foreach (var prop in columns.EnumerateObject()) {
					result[prop.Name] = InferGeoTypeFromColumnMetadata(prop.Value);
				}
			} catch {
				return result;
			}

			return result;
		}

		private static TypeTag InferGeoTypeFromColumnMetadata(JsonElement columnMetadata)
		{
			if (columnMetadata.TryGetProperty("type", out var typeNode) &&
				typeNode.ValueKind == JsonValueKind.String) {
				var typeName = typeNode.GetString() ?? string.Empty;
				if (typeName.Contains("geography", StringComparison.OrdinalIgnoreCase)) {
					return TypeTag.Geography;
				}
				if (typeName.Contains("geometry", StringComparison.OrdinalIgnoreCase)) {
					return TypeTag.Geometry;
				}
			}

			if (columnMetadata.TryGetProperty("edges", out var edgesNode) &&
				edgesNode.ValueKind == JsonValueKind.String) {
				var edges = edgesNode.GetString() ?? string.Empty;
				if (!edges.Equals("planar", StringComparison.OrdinalIgnoreCase) &&
					!edges.Equals("linear", StringComparison.OrdinalIgnoreCase)) {
					return TypeTag.Geography;
				}
			}

			if (columnMetadata.TryGetProperty("edge_interpolation", out var interpolationNode) &&
				interpolationNode.ValueKind == JsonValueKind.String) {
				var interpolation = interpolationNode.GetString() ?? string.Empty;
				if (!string.IsNullOrWhiteSpace(interpolation) &&
					!interpolation.Equals("planar", StringComparison.OrdinalIgnoreCase) &&
					!interpolation.Equals("linear", StringComparison.OrdinalIgnoreCase)) {
					return TypeTag.Geography;
				}
			}

			return TypeTag.Geometry;
		}

		public void SetDataSource(IDataSource source) => _source = source;
	}
}