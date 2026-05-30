using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Unlocks;

namespace RemoveMultiplayerPlayerLimit.Network;

public struct RmpLobbyPlayerState : IPacketSerializable
{
	public ulong id;

	public int slotId;

	public CharacterModel character;

	public SerializableUnlockState unlockState;

	public int maxMultiplayerAscensionUnlocked;

	public bool isReady;

	public readonly LobbyPlayer ToLobbyPlayer()
	{
		return new LobbyPlayer
		{
			id = id,
			slotId = slotId,
			character = character,
			unlockState = unlockState,
			maxMultiplayerAscensionUnlocked = maxMultiplayerAscensionUnlocked,
			isReady = isReady
		};
	}

	public static RmpLobbyPlayerState FromLobbyPlayer(LobbyPlayer lobbyPlayer)
	{
		return new RmpLobbyPlayerState
		{
			id = lobbyPlayer.id,
			slotId = lobbyPlayer.slotId,
			character = lobbyPlayer.character,
			unlockState = lobbyPlayer.unlockState,
			maxMultiplayerAscensionUnlocked = lobbyPlayer.maxMultiplayerAscensionUnlocked,
			isReady = lobbyPlayer.isReady
		};
	}

	public readonly void Serialize(PacketWriter writer)
	{
		writer.WriteULong(id, 64);
		writer.WriteInt(slotId, 4);
		PacketWriterExtensions.WriteModel<CharacterModel>(writer, character);
		writer.Write<SerializableUnlockState>(unlockState);
		writer.WriteInt(maxMultiplayerAscensionUnlocked, 32);
		writer.WriteBool(isReady);
	}

	public void Deserialize(PacketReader reader)
	{
		id = reader.ReadULong(64);
		slotId = reader.ReadInt(4);
		character = PacketReaderExtensions.ReadModel<CharacterModel>(reader);
		unlockState = reader.Read<SerializableUnlockState>();
		maxMultiplayerAscensionUnlocked = reader.ReadInt(32);
		isReady = reader.ReadBool();
	}
}
