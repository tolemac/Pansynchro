using Pansynchro.Core.DataDict;

namespace Pansynchro.Core.CustomTypes
{
	public interface ICustomTypeAccessor
	{
		TypeTag Type { get; }
		string Provider { get; }
		object ToCanonical(object providerValue);
		object FromCanonical(object canonicalValue);
	}
}