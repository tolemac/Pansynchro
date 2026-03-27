using System.IO;

using Pansynchro.Core.DataDict;

namespace Pansynchro.Core.CustomTypes
{
	public interface IProtocolCustomType
	{
		string Name { get; }
		TypeTag Type { get; }
		object ProtocolReader(BinaryReader r);
		void ProtocolWriter(object o, BinaryWriter s);
	}
}
