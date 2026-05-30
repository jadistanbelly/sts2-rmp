using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace RemoveMultiplayerPlayerLimit.Network;

public struct RmpConfigSyncMessage : INetMessage, IPacketSerializable
{
	public int ProtocolVersion;

	public int MaxPlayerLimit;

	public readonly bool ShouldBroadcast => true;

	public readonly bool ShouldBuffer => false;

	public readonly NetTransferMode Mode => (NetTransferMode)2;

	public readonly LogLevel LogLevel => (LogLevel)3;

	public readonly void Serialize(PacketWriter writer)
	{
		writer.WriteInt(ProtocolVersion, 8);
		writer.WriteInt(MaxPlayerLimit, 8);
	}

	public void Deserialize(PacketReader reader)
	{
		ProtocolVersion = reader.ReadInt(8);
		MaxPlayerLimit = reader.ReadInt(8);
	}

	public override readonly string ToString()
	{
		return $"RmpConfigSync(v{ProtocolVersion}, maxPlayers={MaxPlayerLimit})";
	}
}
