using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

using Pansynchro.Core.DataDict;
using Pansynchro.Core.DataDict.TypeSystem;

namespace Pansynchro.Core.CustomTypes
{
	public static class CustomTypeAccessorTransformations
	{
		public static DataStream ApplyForRead(DataStream data, StreamDefinition stream, string provider)
		{
			if (string.IsNullOrWhiteSpace(provider)) {
				return data;
			}

			var handlers = stream.Fields
				.Select((field, idx) => (field, idx))
				.Select(pair => (pair.idx, handler: ResolveAccessor(pair.field.Type, provider)))
				.Where(pair => pair.handler != null)
				.Select(pair => (pair.idx, pair.handler!))
				.ToArray();

			if (handlers.Length == 0) {
				return data;
			}

			return data.Transformed(source => ApplyReadAccessors(source, handlers), stream);
		}

		public static DataStream ApplyForWrite(DataStream data, StreamDefinition stream, string provider)
		{
			if (string.IsNullOrWhiteSpace(provider)) {
				return data;
			}

			var handlers = stream.Fields
				.Select((field, idx) => (field, idx))
				.Select(pair => (pair.idx, handler: ResolveAccessor(pair.field.Type, provider)))
				.Where(pair => pair.handler != null)
				.Select(pair => (pair.idx, pair.handler!))
				.ToArray();

			if (handlers.Length == 0) {
				return data;
			}

			return data.Transformed(source => ApplyWriteAccessors(source, handlers), stream);
		}

		public static Dictionary<string, Func<object, object>> BuildWriteConverters(StreamDefinition stream, string provider)
		{
			var result = new Dictionary<string, Func<object, object>>(StringComparer.InvariantCultureIgnoreCase);
			if (string.IsNullOrWhiteSpace(provider)) {
				return result;
			}

			foreach (var field in stream.Fields) {
				if (field.Type is BasicField bf) {
					var accessor = CustomTypeRegistry.GetTypeAccessor(bf.Type, provider);
					if (accessor != null) {
						result[field.Name] = accessor.FromCanonical;
					}
				}
			}

			return result;
		}

		private static ICustomTypeAccessor? ResolveAccessor(IFieldType type, string provider)
		{
			if (type is BasicField bf) {
				return CustomTypeRegistry.GetTypeAccessor(bf.Type, provider);
			}
			return null;
		}

		private static IEnumerable<object[]> ApplyReadAccessors(IDataReader source, (int idx, ICustomTypeAccessor accessor)[] handlers)
		{
			try {
				var row = new object[source.FieldCount];
				while (source.Read()) {
					source.GetValues(row);
					foreach (var (idx, accessor) in handlers) {
						var value = row[idx];
						if (value is not null && value != DBNull.Value) {
							row[idx] = accessor.ToCanonical(value);
						}
					}
					yield return row;
				}
			} finally {
				source.Dispose();
			}
		}

		private static IEnumerable<object[]> ApplyWriteAccessors(IDataReader source, (int idx, ICustomTypeAccessor accessor)[] handlers)
		{
			try {
				var row = new object[source.FieldCount];
				while (source.Read()) {
					source.GetValues(row);
					foreach (var (idx, accessor) in handlers) {
						var value = row[idx];
						if (value is not null && value != DBNull.Value) {
							row[idx] = accessor.FromCanonical(value);
						}
					}
					yield return row;
				}
			} finally {
				source.Dispose();
			}
		}
	}
}