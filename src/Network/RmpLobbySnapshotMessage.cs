using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace RemoveMultiplayerPlayerLimit.Network;

public struct RmpLobbySnapshotMessage : INetMessage, IPacketSerializable
{
	public List<RmpLobbyPlayerState>? players;

	public readonly bool ShouldBroadcast => false;

	public readonly bool ShouldBuffer => false;

	public readonly NetTransferMode Mode => (NetTransferMode)2;

	public readonly LogLevel LogLevel => (LogLevel)3;

	public readonly void Serialize(PacketWriter writer)
	{
		if (players == null)
		{
			throw new InvalidOperationException("players must not be null");
		}
		writer.WriteList<RmpLobbyPlayerState>((IReadOnlyList<RmpLobbyPlayerState>)players, 5);
	}

	public void Deserialize(PacketReader reader)
	{
		players = reader.ReadList<RmpLobbyPlayerState>(5);
	}

	public override readonly string ToString()
	{
		return $"RmpLobbySnapshot(players={players?.Count ?? 0})";
	}
}
