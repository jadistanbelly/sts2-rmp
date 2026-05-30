using System.Collections.Generic;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using RemoveMultiplayerPlayerLimit.Core;
using RemoveMultiplayerPlayerLimit.Infrastructure;

namespace RemoveMultiplayerPlayerLimit.Network;

public partial class LobbyManagerModule : IRMPModule
{
	private partial class LobbyManagerNode : Node
	{



		private readonly LobbyManagerModule _module;

		private int _frameCounter;

		private StartRunLobby? _lastLobby;

		private LoadRunLobby? _lastLoggedLoadLobby;

		private LoadRunLobby? _lastSyncedLoadLobby;

		private int _lastPlayerCount = -1;

		private int _lastTargetPlayerLimit = -1;

		private int _lastLoadConnectedPlayerCount = -1;

		public LobbyManagerNode(LobbyManagerModule module)
		{
			_module = module;
			((Node)this).Name = new StringName("LobbyManagerNode");
		}

		public override void _Process(double delta)
		{
			if (++_frameCounter % 15 == 0)
			{
				HandleStartRunLobby(SceneMonitor.FindActiveStartRunLobby());
				HandleLoadedRunLobby(SceneMonitor.FindActiveLoadRunLobby());
			}
		}

		private void HandleStartRunLobby(StartRunLobby? lobby)
		{
			if (lobby != _lastLobby)
			{
				if (_lastLobby != null && lobby == null)
				{
					RmpProtocol.Unbind();
				}
				_lastLobby = lobby;
				_lastPlayerCount = -1;
				_lastTargetPlayerLimit = -1;
				if (lobby != null)
				{
					OnLobbyActivated(lobby);
				}
			}
			if (lobby != null)
			{
				int lobbyPlayerCount = GetLobbyPlayerCount(lobby);
				int num = ProtocolConfig.TargetPlayerLimit;
				if (lobbyPlayerCount != _lastPlayerCount || num != _lastTargetPlayerLimit)
				{
					SyncLobbyState(lobby, num);
					_lastPlayerCount = lobbyPlayerCount;
					_lastTargetPlayerLimit = num;
				}
			}
		}

		private void OnLobbyActivated(StartRunLobby lobby)
		{
			INetGameService netService = lobby.NetService;
			NetGameType? val = ((netService != null) ? new NetGameType?(netService.Type) : ((NetGameType?)null));
			bool flag;
			if (val.HasValue)
			{
				NetGameType valueOrDefault = val.GetValueOrDefault();
				if (NetGameTypeExtensions.IsMultiplayer(valueOrDefault))
				{
					flag = true;
					goto IL_003d;
				}
			}
			flag = false;
			goto IL_003d;
			IL_003d:
			if (flag)
			{
				RmpProtocol.Bind(lobby.NetService);
			}
			SyncLobbyState(lobby, ProtocolConfig.TargetPlayerLimit);
		}

		private void SyncLobbyState(StartRunLobby lobby, int targetLimit)
		{
			if (_module._maxPlayersField != null && lobby.MaxPlayers != targetLimit)
			{
				_module._maxPlayersField.SetValue(lobby, targetLimit);
				Log.Info($"[RMP] StartRunLobby.MaxPlayers synchronized to {targetLimit}", 2);
			}
			INetGameService netService = lobby.NetService;
			if (netService != null && (int)netService.Type == 2)
			{
				LobbyCapacitySync.TrySyncSteamLimit(
					() => SteamLobbyHelper.GetCurrentMemberLimit(lobby.NetService),
					limit => SteamLobbyHelper.TryUpdateMemberLimit(lobby.NetService, limit),
					targetLimit);
				RmpProtocol.BroadcastConfig(targetLimit);
				if (ExtendedLobbyModule.ShouldUseExtendedLobbyProtocol(lobby))
				{
					RmpProtocol.BroadcastLobbySnapshot(lobby.Players);
				}
			}
		}

		private void HandleLoadedRunLobby(LoadRunLobby? loadRunLobby)
		{
			if (loadRunLobby == null)
			{
				_lastLoggedLoadLobby = null;
				_lastSyncedLoadLobby = null;
				_lastLoadConnectedPlayerCount = -1;
			}
			else
			{
				int count = loadRunLobby.Run.Players.Count;
				int count2 = loadRunLobby.ConnectedPlayerIds.Count;
				if (loadRunLobby != _lastLoggedLoadLobby)
				{
					_lastLoggedLoadLobby = loadRunLobby;
					int capacity;
					int value = (HostBootstrapModule.TryGetTrackedHostCapacity(loadRunLobby.NetService, out capacity) ? capacity : count);
					string value2 = SceneMonitor.GetActiveLoadLobbyScreenName() ?? "UnknownLoadLobbyScreen";
					Log.Info($"[RMP] Loaded-run lobby active: screen={value2}, transport={HostBootstrapModule.GetTransportName(loadRunLobby.NetService)}, hostCapacity={value}, savePlayers={count}, connectedPlayers={count2}", 2);
				}
				SyncLoadedRunHostState(loadRunLobby, count, count2, ProtocolConfig.TargetPlayerLimit);
			}
		}

		private void SyncLoadedRunHostState(LoadRunLobby loadRunLobby, int savePlayerCount, int connectedPlayerCount, int targetLimit)
		{
			INetGameService netService = loadRunLobby.NetService;
			if (netService == null || netService.Type != NetGameType.Host)
			{
				return;
			}

			bool firstSync = loadRunLobby != _lastSyncedLoadLobby;
			if (firstSync)
			{
				RmpProtocol.Bind(netService);
				_lastSyncedLoadLobby = loadRunLobby;
			}

			int steamLimitBefore = SteamLobbyHelper.GetCurrentMemberLimit(netService);
			bool changed = LobbyCapacitySync.TrySyncSteamLimit(
				() => steamLimitBefore,
				limit => SteamLobbyHelper.TryUpdateMemberLimit(netService, limit),
				targetLimit);
			int steamLimitAfter = changed ? SteamLobbyHelper.GetCurrentMemberLimit(netService) : steamLimitBefore;

			if (firstSync || changed || connectedPlayerCount != _lastLoadConnectedPlayerCount)
			{
				Log.Info($"[RMP] Loaded-run lobby capacity sync: target={targetLimit}, steamBefore={steamLimitBefore}, steamAfter={steamLimitAfter}, changed={changed}, savePlayers={savePlayerCount}, connectedPlayers={connectedPlayerCount}", 2);
				RmpProtocol.BroadcastConfig(targetLimit);
				_lastLoadConnectedPlayerCount = connectedPlayerCount;
			}
		}

		private static int GetLobbyPlayerCount(StartRunLobby lobby)
		{
			try
			{
				return lobby.Players?.Count ?? 0;
			}
			catch
			{
				return 0;
			}
		}








	}

	private FieldInfo? _maxPlayersField;

	public string Name => "LobbyManager";

	public void Initialize(ConfigManager config, ReflectionCache cache)
	{
		_maxPlayersField = cache.GetField(typeof(StartRunLobby), "<MaxPlayers>k__BackingField");
	}

	public Node? CreateNode()
	{
		return new LobbyManagerNode(this);
	}

	public void Cleanup()
	{
		RmpProtocol.Unbind();
	}
}
