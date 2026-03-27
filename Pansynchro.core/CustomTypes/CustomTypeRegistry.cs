using System.Collections.Generic;

using Pansynchro.Core.DataDict;

namespace Pansynchro.Core.CustomTypes
{
	public static class CustomTypeRegistry
	{
		private static Dictionary<TypeTag, IProtocolCustomType> _protocolRegistry = new();
		private static Dictionary<(TypeTag type, string provider), ICustomTypeAccessor> _accessorRegistry = new();

		public static void RegisterProtocolType(IProtocolCustomType type)
		{
			var tag = type.Type;
			_protocolRegistry.Add(tag, type);
		}

		public static IProtocolCustomType? GetProtocolType(TypeTag type)
		{
			_protocolRegistry.TryGetValue(type, out var result);
			return result;
		}

		public static void RegisterTypeAccessor(ICustomTypeAccessor type)
		{
			_accessorRegistry[(type.Type, type.Provider.ToUpperInvariant())] = type;
		}

		public static ICustomTypeAccessor? GetTypeAccessor(TypeTag type, string provider)
		{
			_accessorRegistry.TryGetValue((type, provider.ToUpperInvariant()), out var result);
			return result;
		}
	}
}
