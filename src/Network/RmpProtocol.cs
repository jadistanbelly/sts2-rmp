using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Saves.Runs;
using RemoveMultiplayerPlayerLimit.Infrastructure;

namespace RemoveMultiplayerPlayerLimit.Network;

public static class RmpProtocol
{
	public const int ProtocolVersion = 1;

	private static INetGameService? _netService;

	public static bool IsActive => _netService != null;

	public static void Bind(INetGameService netService)
	{
		Unbind();
		_netService = netService;
		netService.RegisterMessageHandler<RmpConfigSyncMessage>((MessageHandlerDelegate<RmpConfigSyncMessage>)HandleConfigSync);
		netService.RegisterMessageHandler<RmpLobbySnapshotMessage>((MessageHandlerDelegate<RmpLobbySnapshotMessage>)HandleLobbySnapshot);
		netService.RegisterMessageHandler<RmpExtendedReadyStateMessage>((MessageHandlerDelegate<RmpExtendedReadyStateMessage>)HandleExtendedReadyState);
		netService.RegisterMessageHandler<RmpExtendedBeginRunMessage>((MessageHandlerDelegate<RmpExtendedBeginRunMessage>)HandleExtendedBeginRun);
		Log.Info($"[RMP] Protocol v{1} bound to {netService.Type} (NetId={netService.NetId})", 2);
	}

	public static void Unbind()
	{
		if (_netService != null)
		{
			try
			{
				_netService.UnregisterMessageHandler<RmpConfigSyncMessage>((MessageHandlerDelegate<RmpConfigSyncMessage>)HandleConfigSync);
				_netService.UnregisterMessageHandler<RmpLobbySnapshotMessage>((MessageHandlerDelegate<RmpLobbySnapshotMessage>)HandleLobbySnapshot);
				_netService.UnregisterMessageHandler<RmpExtendedReadyStateMessage>((MessageHandlerDelegate<RmpExtendedReadyStateMessage>)HandleExtendedReadyState);
				_netService.UnregisterMessageHandler<RmpExtendedBeginRunMessage>((MessageHandlerDelegate<RmpExtendedBeginRunMessage>)HandleExtendedBeginRun);
			}
			catch
			{
			}
			_netService = null;
		}
	}

	public static void BroadcastConfig(int maxPlayerLimit)
	{
		if (_netService != null && (int)_netService.Type == 2)
		{
			_netService.SendMessage<RmpConfigSyncMessage>(new RmpConfigSyncMessage
			{
				ProtocolVersion = 1,
				MaxPlayerLimit = maxPlayerLimit
			});
		}
	}

	public static void BroadcastLobbySnapshot(IReadOnlyList<LobbyPlayer> players)
	{
		if (_netService != null && (int)_netService.Type == 2)
		{
			_netService.SendMessage<RmpLobbySnapshotMessage>(new RmpLobbySnapshotMessage
			{
				players = players.Select(RmpLobbyPlayerState.FromLobbyPlayer).ToList()
			});
		}
	}

	public static void BroadcastExtendedReady(bool ready)
	{
		if (_netService != null && (int)_netService.Type == 2)
		{
			_netService.SendMessage<RmpExtendedReadyStateMessage>(new RmpExtendedReadyStateMessage
			{
				Ready = ready
			});
		}
	}

	public static void BroadcastExtendedBeginRun(IReadOnlyList<LobbyPlayer> players, string seed, string act1, IReadOnlyList<ModifierModel> modifiers)
	{
		if (_netService != null && (int)_netService.Type == 2)
		{
			_netService.SendMessage<RmpExtendedBeginRunMessage>(new RmpExtendedBeginRunMessage
			{
				players = players.Select(RmpLobbyPlayerState.FromLobbyPlayer).ToList(),
				seed = seed,
				act1 = act1,
				modifiers = modifiers.Select((ModifierModel modifier) => modifier.ToSerializable()).ToList()
			});
		}
	}

	private static void HandleConfigSync(RmpConfigSyncMessage message, ulong senderId)
	{
		if (message.ProtocolVersion != 1)
		{
			Log.Warn($"[RMP] Protocol version mismatch: local={1}, remote={message.ProtocolVersion} from {senderId}", 2);
		}
		Log.Info($"[RMP] Config sync from {senderId}: v{message.ProtocolVersion}, maxPlayers={message.MaxPlayerLimit} (local fixed at {16})", 2);
	}

	private static void HandleLobbySnapshot(RmpLobbySnapshotMessage message, ulong senderId)
	{
		if (message.players != null)
		{
			StartRunLobby val = SceneMonitor.FindActiveStartRunLobby();
			if (val != null)
			{
				ApplyLobbySnapshot(val, message.players);
			}
		}
	}

	private static void HandleExtendedReadyState(RmpExtendedReadyStateMessage message, ulong senderId)
	{
		StartRunLobby val = SceneMonitor.FindActiveStartRunLobby();
		if (val != null && ExtendedLobbyModule.ShouldUseExtendedLobbyProtocol(val))
		{
			if (ExtendedLobbyModule.TrySetPlayerReadyState(val, senderId, message.Ready, out var updatedPlayer))
			{
				ExtendedLobbyModule.NotifyPlayerChanged(val, updatedPlayer, isRandomCharacterResolution: false);
			}
			if ((int)val.NetService.Type == 2)
			{
				ExtendedLobbyModule.TryBeginExtendedRun(val);
			}
		}
	}

	private static void HandleExtendedBeginRun(RmpExtendedBeginRunMessage message, ulong senderId)
	{
		if (message.players == null)
		{
			return;
		}
		StartRunLobby val = SceneMonitor.FindActiveStartRunLobby();
		if (val != null)
		{
			ApplyLobbySnapshot(val, message.players);
			List<ModifierModel> list = ((IEnumerable<SerializableModifier>)message.modifiers).Select((Func<SerializableModifier, ModifierModel>)ModifierModel.FromSerializable).ToList();
			List<ActModel> list2 = ExtendedLobbyModule.BuildActsForBeginRun(message.seed, message.act1, val, message.players.Select((RmpLobbyPlayerState player) => player.ToLobbyPlayer()).ToList());
			val.LobbyListener.BeginRun(message.seed, list2, (IReadOnlyList<ModifierModel>)list);
		}
	}

	private static void ApplyLobbySnapshot(StartRunLobby lobby, IReadOnlyList<RmpLobbyPlayerState> snapshotPlayers)
	{
		Dictionary<ulong, LobbyPlayer> dictionary = lobby.Players.ToDictionary((LobbyPlayer player) => player.id);
		List<LobbyPlayer> list = snapshotPlayers.Select((RmpLobbyPlayerState player) => player.ToLobbyPlayer()).ToList();
		HashSet<ulong> hashSet = list.Select((LobbyPlayer player) => player.id).ToHashSet();
		for (int num = lobby.Players.Count - 1; num >= 0; num--)
		{
			LobbyPlayer val = lobby.Players[num];
			if (!hashSet.Contains(val.id))
			{
				lobby.Players.RemoveAt(num);
				if (val.id != lobby.NetService.NetId)
				{
					lobby.LobbyListener.RemotePlayerDisconnected(val);
				}
			}
		}
		foreach (LobbyPlayer snapshotPlayer in list)
		{
			if (!dictionary.TryGetValue(snapshotPlayer.id, out var value))
			{
				lobby.Players.Add(snapshotPlayer);
				if (snapshotPlayer.id != lobby.NetService.NetId)
				{
					lobby.LobbyListener.PlayerConnected(snapshotPlayer);
				}
			}
			else if (!LobbyPlayersEqual(value, snapshotPlayer))
			{
				int num2 = lobby.Players.FindIndex((LobbyPlayer player) => player.id == snapshotPlayer.id);
				if (num2 >= 0)
				{
					lobby.Players[num2] = snapshotPlayer;
				}
				ExtendedLobbyModule.NotifyPlayerChanged(lobby, snapshotPlayer, isRandomCharacterResolution: false);
			}
		}
		NGame instance = NGame.Instance;
		if (instance != null)
		{
			instance.RemoteCursorContainer.Initialize(lobby.InputSynchronizer, lobby.Players.Select((LobbyPlayer player) => player.id));
		}
	}

	private static bool LobbyPlayersEqual(LobbyPlayer a, LobbyPlayer b)
	{
		if (a.id == b.id && a.slotId == b.slotId && a.character == b.character && a.maxMultiplayerAscensionUnlocked == b.maxMultiplayerAscensionUnlocked)
		{
			return a.isReady == b.isReady;
		}
		return false;
	}
}
