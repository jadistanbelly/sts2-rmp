using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Ftue;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.DailyRun;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;
using RemoveMultiplayerPlayerLimit.Core;
using RemoveMultiplayerPlayerLimit.Infrastructure;

namespace RemoveMultiplayerPlayerLimit.Network;

public partial class ExtendedLobbyModule : IRMPModule
{
	private sealed class HostJoinPatchState
	{
		public required StartRunLobby Lobby { get; init; }

		public required MessageHandlerDelegate<ClientLobbyJoinRequestMessage> OriginalJoinHandler { get; init; }

		public required MessageHandlerDelegate<ClientLobbyJoinRequestMessage> ReplacementJoinHandler { get; init; }
	}

	private sealed partial class ExtendedLobbyNode : Node
	{



		private readonly HashSet<ulong> _patchedStandardScreens = new HashSet<ulong>();

		private readonly HashSet<ulong> _patchedCustomScreens = new HashSet<ulong>();

		private readonly HashSet<ulong> _patchedDailyScreens = new HashSet<ulong>();

		private int _frameCounter;

		public ExtendedLobbyNode()
		{
			((Node)this).Name = new StringName("ExtendedLobbyNode");
		}

		public override void _Process(double delta)
		{
			if (++_frameCounter % 5 == 0)
			{
				StartRunLobby val = SceneMonitor.FindActiveStartRunLobby();
				if (val != null && (int)val.NetService.Type == 2)
				{
					EnsureHostJoinPatch(val);
				}
				PatchStandardScreen(SceneRegistry.Instance?.CharacterSelectScreen);
				PatchCustomScreen(SceneRegistry.Instance?.CustomRunScreen);
				PatchDailyScreen(SceneRegistry.Instance?.DailyRunScreen);
			}
		}

		private void PatchStandardScreen(NCharacterSelectScreen? screen)
		{
			if (screen == null)
			{
				return;
			}
			ulong instanceId = ((GodotObject)screen).GetInstanceId();
			if (!_patchedStandardScreens.Contains(instanceId))
			{
				ReplaceReleasedHandler(((Node)screen).GetNode<NButton>(new NodePath("ConfirmButton")), screen, "OnEmbarkPressed", delegate(NButton button)
				{
					OnStandardEmbarkPressed(screen, button);
				});
				ReplaceReleasedHandler(((Node)screen).GetNode<NButton>(new NodePath("UnreadyButton")), screen, "OnUnreadyPressed", delegate(NButton button)
				{
					OnStandardUnreadyPressed(screen, button);
				});
				_patchedStandardScreens.Add(instanceId);
			}
		}

		private void PatchCustomScreen(NCustomRunScreen? screen)
		{
			if (screen == null)
			{
				return;
			}
			ulong instanceId = ((GodotObject)screen).GetInstanceId();
			if (!_patchedCustomScreens.Contains(instanceId))
			{
				ReplaceReleasedHandler(((Node)screen).GetNode<NButton>(new NodePath("ConfirmButton")), screen, "OnEmbarkPressed", delegate(NButton button)
				{
					OnCustomEmbarkPressed(screen, button);
				});
				ReplaceReleasedHandler(((Node)screen).GetNode<NButton>(new NodePath("UnreadyButton")), screen, "OnUnreadyPressed", delegate(NButton button)
				{
					OnCustomUnreadyPressed(screen, button);
				});
				_patchedCustomScreens.Add(instanceId);
			}
		}

		private void PatchDailyScreen(NDailyRunScreen? screen)
		{
			if (screen == null)
			{
				return;
			}
			ulong instanceId = ((GodotObject)screen).GetInstanceId();
			if (!_patchedDailyScreens.Contains(instanceId))
			{
				ReplaceReleasedHandler(((Node)screen).GetNode<NButton>(new NodePath("%ConfirmButton")), screen, "OnEmbarkPressed", delegate(NButton button)
				{
					OnDailyEmbarkPressed(screen, button);
				});
				ReplaceReleasedHandler(((Node)screen).GetNode<NButton>(new NodePath("%UnreadyButton")), screen, "OnUnreadyPressed", delegate(NButton button)
				{
					OnDailyUnreadyPressed(screen, button);
				});
				_patchedDailyScreens.Add(instanceId);
			}
		}

		private void EnsureHostJoinPatch(StartRunLobby lobby)
		{
			ulong hashCodeAsUlong = lobby.GetHashCodeAsUlong();
			if (!HostJoinPatchStates.ContainsKey(hashCodeAsUlong) && !(StartRunHandleJoinMethod == null))
			{
				MessageHandlerDelegate<ClientLobbyJoinRequestMessage> originalHandler = (MessageHandlerDelegate<ClientLobbyJoinRequestMessage>)(object)Delegate.CreateDelegate(typeof(MessageHandlerDelegate<ClientLobbyJoinRequestMessage>), lobby, StartRunHandleJoinMethod);
				MessageHandlerDelegate<ClientLobbyJoinRequestMessage> val = delegate(ClientLobbyJoinRequestMessage message, ulong senderId)
				{
					HandleExtendedJoinRequest(lobby, originalHandler, message, senderId);
				};
				lobby.NetService.UnregisterMessageHandler<ClientLobbyJoinRequestMessage>(originalHandler);
				lobby.NetService.RegisterMessageHandler<ClientLobbyJoinRequestMessage>(val);
				HostJoinPatchStates[hashCodeAsUlong] = new HostJoinPatchState
				{
					Lobby = lobby,
					OriginalJoinHandler = originalHandler,
					ReplacementJoinHandler = val
				};
			}
		}

		private static void HandleExtendedJoinRequest(StartRunLobby lobby, MessageHandlerDelegate<ClientLobbyJoinRequestMessage> originalHandler, ClientLobbyJoinRequestMessage message, ulong senderId)
		{
			int num = lobby.Players.Count + 1;
			if (!ShouldUseExtendedLobbyProtocol(lobby) || num <= 7)
			{
				originalHandler.Invoke(message, senderId);
				return;
			}
			if ((int)lobby.NetService.Type == 2)
			{
				INetGameService netService = lobby.NetService;
				NetHostGameService val = (NetHostGameService)(object)((netService is NetHostGameService) ? netService : null);
				if (val != null)
				{
					if (lobby.Players.Count >= lobby.MaxPlayers)
					{
						val.DisconnectClient(senderId, (NetError)7, false);
						return;
					}
					try
					{
						LobbyPlayer? val2 = (LobbyPlayer?)TryAddPlayerMethod?.Invoke(lobby, new object[3] { message.unlockState, message.maxAscensionUnlocked, senderId });
						if (!val2.HasValue)
						{
							val.DisconnectClient(senderId, (NetError)17, false);
							return;
						}
						UpdateMaxAscensionMethod?.Invoke(lobby, null);
						ClientLobbyJoinResponseMessage val3 = new ClientLobbyJoinResponseMessage
						{
							playersInLobby = BuildJoinResponsePlayers(lobby.Players, senderId),
							ascension = lobby.Ascension,
							dailyTime = lobby.DailyTime,
							seed = lobby.Seed,
							modifiers = lobby.Modifiers.Select((ModifierModel modifier) => modifier.ToSerializable()).ToList()
						};
						val.SendMessage<ClientLobbyJoinResponseMessage>(val3, senderId);
						val.SetPeerReadyForBroadcasting(senderId);
						PlayerJoinedMessage val4 = new PlayerJoinedMessage
						{
							lobbyPlayer = val2.Value
						};
						foreach (LobbyPlayer player in lobby.Players)
						{
							if (player.id != lobby.NetService.NetId && player.id != senderId)
							{
								lobby.NetService.SendMessage<PlayerJoinedMessage>(val4, player.id);
							}
						}
						RemoveConnectingPlayerMethod?.Invoke(lobby, new object[1] { senderId });
						lobby.LobbyListener.PlayerConnected(val2.Value);
						RmpProtocol.BroadcastLobbySnapshot(lobby.Players);
						return;
					}
					catch (Exception value)
					{
						val.DisconnectClient(senderId, (NetError)17, false);
						Log.Error($"[RMP:ExtendedLobby] Failed to process extended join request: {value}", 2);
						return;
					}
				}
			}
			throw new InvalidOperationException("Extended join request received as non-host.");
		}

		private static List<LobbyPlayer> BuildJoinResponsePlayers(IReadOnlyList<LobbyPlayer> players, ulong joiningPlayerId)
		{
			List<LobbyPlayer> list = players.Take(7).ToList();
			if (list.Any((LobbyPlayer player) => player.id == joiningPlayerId))
			{
				return list;
			}
			LobbyPlayer val = players.First((LobbyPlayer player) => player.id == joiningPlayerId);
			if (list.Count == 7)
			{
				int num = list.FindLastIndex((LobbyPlayer player) => player.id != players[0].id);
				if (num < 0)
				{
					num = list.Count - 1;
				}
				list[num] = val;
			}
			else
			{
				list.Add(val);
			}
			return list;
		}

		private static void ReplaceReleasedHandler(NButton button, object target, string originalMethodName, Action<NButton> replacement)
		{
			Callable val = CreatePrivateReleasedCallable(target, originalMethodName);
			if (((GodotObject)button).IsConnected(NClickableControl.SignalName.Released, val))
			{
				((GodotObject)button).Disconnect(NClickableControl.SignalName.Released, val);
			}
			((GodotObject)button).Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(replacement), 0u);
		}

		private static Callable CreatePrivateReleasedCallable(object target, string methodName)
		{
			MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new MissingMethodException(target.GetType().FullName, methodName);
			return Callable.From<NButton>((Action<NButton>)Delegate.CreateDelegate(typeof(Action<NButton>), target, method));
		}

		private static void OnStandardEmbarkPressed(NCharacterSelectScreen screen, NButton button)
		{
			StartRunLobby lobby = screen.Lobby;
			if (!ShouldUseExtendedLobbyProtocol(lobby) || lobby.Players.Count <= 7)
			{
				InvokePrivateButtonMethod(screen, "OnEmbarkPressed", button);
				return;
			}
			if (!SaveManager.Instance.SeenFtue("accept_tutorials_ftue"))
			{
				if (NModalContainer.Instance != null)
				{
					NAcceptTutorialsFtue val = NAcceptTutorialsFtue.Create(screen, (Action)delegate
					{
						OnStandardEmbarkPressed(screen, button);
					});
					if (val != null)
					{
						NModalContainer.Instance.Add((Node)(object)val, true);
					}
				}
				return;
			}
			((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("ConfirmButton"))).Disable();
			((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("BackButton"))).Disable();
			lobby.Act1 = ((Node)screen).GetNode<NActDropdown>(new NodePath("%ActDropdown")).CurrentOption;
			SetLocalReady((Node)(object)screen, lobby, ready: true);
			foreach (NCharacterSelectButton item in ((IEnumerable)((Node)((Node)screen).GetNode<Control>(new NodePath("CharSelectButtons/ButtonContainer"))).GetChildren(false)).OfType<NCharacterSelectButton>())
			{
				((NClickableControl)item).Disable();
			}
			if (!lobby.Players.All((LobbyPlayer player) => player.isReady))
			{
				((CanvasItem)((Node)screen).GetNode<Control>(new NodePath("ReadyAndWaitingPanel"))).Visible = true;
				((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("UnreadyButton"))).Enable();
			}
		}

		private static void OnStandardUnreadyPressed(NCharacterSelectScreen screen, NButton button)
		{
			StartRunLobby lobby = screen.Lobby;
			if (!ShouldUseExtendedLobbyProtocol(lobby) || lobby.Players.Count <= 7)
			{
				InvokePrivateButtonMethod(screen, "OnUnreadyPressed", button);
				return;
			}
			((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("ConfirmButton"))).Enable();
			((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("BackButton"))).Enable();
			((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("UnreadyButton"))).Disable();
			((CanvasItem)((Node)screen).GetNode<Control>(new NodePath("ReadyAndWaitingPanel"))).Visible = false;
			foreach (NCharacterSelectButton item in ((IEnumerable)((Node)((Node)screen).GetNode<Control>(new NodePath("CharSelectButtons/ButtonContainer"))).GetChildren(false)).OfType<NCharacterSelectButton>())
			{
				((NClickableControl)item).Enable();
			}
			SetLocalReady((Node)(object)screen, lobby, ready: false);
		}

		private static void OnCustomEmbarkPressed(NCustomRunScreen screen, NButton button)
		{
			StartRunLobby lobby = screen.Lobby;
			if (!ShouldUseExtendedLobbyProtocol(lobby) || lobby.Players.Count <= 7)
			{
				InvokePrivateButtonMethod(screen, "OnEmbarkPressed", button);
				return;
			}
			((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("ConfirmButton"))).Disable();
			((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("BackButton"))).Disable();
			foreach (NCharacterSelectButton item in ((IEnumerable)((Node)((Node)screen).GetNode<Control>(new NodePath("LeftContainer/CharSelectButtons/ButtonContainer"))).GetChildren(false)).OfType<NCharacterSelectButton>())
			{
				((NClickableControl)item).Disable();
			}
			SetLocalReady((Node)(object)screen, lobby, ready: true);
			if (!lobby.Players.All((LobbyPlayer player) => player.isReady))
			{
				((CanvasItem)((Node)screen).GetNode<Control>(new NodePath("%ReadyAndWaitingPanel"))).Visible = true;
				((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("UnreadyButton"))).Enable();
			}
		}

		private static void OnCustomUnreadyPressed(NCustomRunScreen screen, NButton button)
		{
			StartRunLobby lobby = screen.Lobby;
			if (!ShouldUseExtendedLobbyProtocol(lobby) || lobby.Players.Count <= 7)
			{
				InvokePrivateButtonMethod(screen, "OnUnreadyPressed", button);
				return;
			}
			((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("ConfirmButton"))).Enable();
			((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("BackButton"))).Enable();
			((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("UnreadyButton"))).Disable();
			((CanvasItem)((Node)screen).GetNode<Control>(new NodePath("%ReadyAndWaitingPanel"))).Visible = false;
			foreach (NCharacterSelectButton item in ((IEnumerable)((Node)((Node)screen).GetNode<Control>(new NodePath("LeftContainer/CharSelectButtons/ButtonContainer"))).GetChildren(false)).OfType<NCharacterSelectButton>())
			{
				((NClickableControl)item).Enable();
			}
			SetLocalReady((Node)(object)screen, lobby, ready: false);
		}

		private static void OnDailyEmbarkPressed(NDailyRunScreen screen, NButton button)
		{
			object? obj = DailyRunLobbyField?.GetValue(screen);
			StartRunLobby val = (StartRunLobby)((obj is StartRunLobby) ? obj : null);
			if (val == null)
			{
				return;
			}
			if (!ShouldUseExtendedLobbyProtocol(val) || val.Players.Count <= 7)
			{
				InvokePrivateButtonMethod(screen, "OnEmbarkPressed", button);
				return;
			}
			((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("%ConfirmButton"))).Disable();
			((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("%BackButton"))).Disable();
			SetLocalReady((Node)(object)screen, val, ready: true);
			if (!val.Players.All((LobbyPlayer player) => player.isReady))
			{
				((CanvasItem)((Node)screen).GetNode<Control>(new NodePath("%ReadyAndWaitingPanel"))).Visible = true;
				((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("%UnreadyButton"))).Enable();
			}
		}

		private static void OnDailyUnreadyPressed(NDailyRunScreen screen, NButton button)
		{
			object? obj = DailyRunLobbyField?.GetValue(screen);
			StartRunLobby val = (StartRunLobby)((obj is StartRunLobby) ? obj : null);
			if (val != null)
			{
				if (!ShouldUseExtendedLobbyProtocol(val) || val.Players.Count <= 7)
				{
					InvokePrivateButtonMethod(screen, "OnUnreadyPressed", button);
					return;
				}
				((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("%ConfirmButton"))).Enable();
				((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("%BackButton"))).Enable();
				((NClickableControl)((Node)screen).GetNode<NButton>(new NodePath("%UnreadyButton"))).Disable();
				((CanvasItem)((Node)screen).GetNode<Control>(new NodePath("%ReadyAndWaitingPanel"))).Visible = false;
				SetLocalReady((Node)(object)screen, val, ready: false);
			}
		}

		private static void SetLocalReady(Node screen, StartRunLobby lobby, bool ready)
		{
			if (TrySetPlayerReadyState(lobby, lobby.NetService.NetId, ready, out var updatedPlayer))
			{
				NotifyPlayerChanged(lobby, updatedPlayer, isRandomCharacterResolution: false);
				if ((int)lobby.NetService.Type == 2)
				{
					RmpProtocol.BroadcastExtendedReady(ready);
					TryBeginExtendedRun(lobby);
				}
				else
				{
					lobby.NetService.SendMessage<RmpExtendedReadyStateMessage>(new RmpExtendedReadyStateMessage
					{
						Ready = ready
					});
				}
			}
		}

		private static void InvokePrivateButtonMethod(object target, string methodName, NButton button)
		{
			(target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new MissingMethodException(target.GetType().FullName, methodName)).Invoke(target, new object[1] { button });
		}









	}

	private static readonly MethodInfo? TryAddPlayerMethod = typeof(StartRunLobby).GetMethod("TryAddPlayerInFirstAvailableSlot", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly MethodInfo? UpdateMaxAscensionMethod = typeof(StartRunLobby).GetMethod("UpdateMaxMultiplayerAscension", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly MethodInfo? RemoveConnectingPlayerMethod = typeof(StartRunLobby).GetMethod("RemoveConnectingPlayer", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly MethodInfo? StartRunHandleJoinMethod = typeof(StartRunLobby).GetMethod("HandleClientLobbyJoinRequestMessage", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly MethodInfo? GetRandomActListMethod = typeof(ActModel).GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault((MethodInfo method) => method.Name == "GetRandomList" && method.GetParameters().Length == 3);

	private static readonly MethodInfo? GenericActMethod = typeof(ModelDb).GetMethod("Act", BindingFlags.Static | BindingFlags.Public);

	private static readonly FieldInfo? DailyRunLobbyField = typeof(NDailyRunScreen).GetField("_lobby", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly HashSet<ulong> ExtendedRunStartingLobbyIds = new HashSet<ulong>();

	private static readonly Dictionary<ulong, HostJoinPatchState> HostJoinPatchStates = new Dictionary<ulong, HostJoinPatchState>();

	public string Name => "ExtendedLobby";

	public void Initialize(ConfigManager config, ReflectionCache cache)
	{
	}

	public Node? CreateNode()
	{
		return new ExtendedLobbyNode();
	}

	public void Cleanup()
	{
		HostJoinPatchStates.Clear();
		ExtendedRunStartingLobbyIds.Clear();
	}

	internal static bool ShouldUseExtendedLobbyProtocol(StartRunLobby lobby)
	{
		return NetGameTypeExtensions.IsMultiplayer(lobby.NetService.Type);
	}

	internal static bool TrySetPlayerReadyState(StartRunLobby lobby, ulong playerId, bool ready, out LobbyPlayer updatedPlayer)
	{
		int num = lobby.Players.FindIndex((LobbyPlayer player) => player.id == playerId);
		if (num < 0)
		{
			updatedPlayer = default(LobbyPlayer);
			return false;
		}
		updatedPlayer = lobby.Players[num];
		updatedPlayer.isReady = ready;
		lobby.Players[num] = updatedPlayer;
		return true;
	}

	internal static List<ActModel> BuildActsForBeginRun(string seed, string act1, StartRunLobby lobby, IReadOnlyList<LobbyPlayer> players)
	{
		UnlockState unlockState = new UnlockState(players.Select((LobbyPlayer player) => UnlockState.FromSerializable(player.unlockState)));
		Rng rng = new Rng((uint)StringHelper.GetDeterministicHashCode(seed), 0);
		List<ActModel> list = InvokeGetRandomActList(seed, rng, unlockState, NetGameTypeExtensions.IsMultiplayer(lobby.NetService.Type));
		ActModel act2 = GetAct(act1);
		if (act2 != null)
		{
			list[0] = act2;
		}
		return list;
	}

	internal static void NotifyPlayerChanged(StartRunLobby lobby, LobbyPlayer player, bool isRandomCharacterResolution)
	{
		MethodInfo method = ((object)lobby.LobbyListener).GetType().GetMethod("PlayerChanged");
		if (!(method == null))
		{
			if (method.GetParameters().Length >= 2)
			{
				method.Invoke(lobby.LobbyListener, new object[2] { player, isRandomCharacterResolution });
			}
			else
			{
				method.Invoke(lobby.LobbyListener, new object[1] { player });
			}
		}
	}

	internal static bool TryBeginExtendedRun(StartRunLobby lobby)
	{
		if (!ShouldUseExtendedLobbyProtocol(lobby) || lobby.Players.Count <= 7 || (int)lobby.NetService.Type != 2)
		{
			return false;
		}
		if (lobby.Players.Count <= 1 || lobby.Players.Any((LobbyPlayer player) => !player.isReady))
		{
			return false;
		}
		ulong hashCodeAsUlong = lobby.GetHashCodeAsUlong();
		if (!ExtendedRunStartingLobbyIds.Add(hashCodeAsUlong))
		{
			return false;
		}
		try
		{
			NGame instance = NGame.Instance;
			string text = ((instance != null) ? instance.DebugSeedOverride : null) ?? (string.IsNullOrWhiteSpace(lobby.Seed) ? SeedHelper.GetRandomSeed(10) : SeedHelper.CanonicalizeSeed(lobby.Seed));
			NormalizeRandomCharacters(lobby, text);
			List<ModifierModel> list = lobby.Modifiers.ToList();
			List<ActModel> list2 = BuildActsForBeginRun(text, lobby.Act1, lobby, lobby.Players);
			RmpProtocol.BroadcastExtendedBeginRun(lobby.Players, text, lobby.Act1, list);
			lobby.LobbyListener.BeginRun(text, list2, (IReadOnlyList<ModifierModel>)list);
			INetGameService netService = lobby.NetService;
			NetHostGameService val = (NetHostGameService)(object)((netService is NetHostGameService) ? netService : null);
			if (val != null)
			{
				NetHost netHost = val.NetHost;
				if (netHost != null)
				{
					netHost.SetHostIsClosed(true);
				}
			}
			Log.Info($"[RMP:ExtendedLobby] Started extended multiplayer run with {lobby.Players.Count} players.", 2);
			return true;
		}
		finally
		{
			ExtendedRunStartingLobbyIds.Remove(hashCodeAsUlong);
		}
	}

	private static void NormalizeRandomCharacters(StartRunLobby lobby, string seed)
	{
		Rng val = new Rng((uint)StringHelper.GetDeterministicHashCode(seed), 0);
		for (int i = 0; i < lobby.Players.Count; i++)
		{
			LobbyPlayer val2 = lobby.Players[i];
			if (val2.character is RandomCharacter)
			{
				val2.character = val.NextItem<CharacterModel>(ModelDb.AllCharacters) ?? ModelDb.AllCharacters.First();
				lobby.Players[i] = val2;
				NotifyPlayerChanged(lobby, val2, isRandomCharacterResolution: true);
			}
		}
	}

	private static ActModel? GetAct(string act1Key)
	{
		string text = ((act1Key == "overgrowth") ? "MegaCrit.Sts2.Core.Models.Acts.Overgrowth" : ((!(act1Key == "underdocks")) ? null : "MegaCrit.Sts2.Core.Models.Acts.Underdocks"));
		string text2 = text;
		if (text2 == null || GenericActMethod == null)
		{
			return null;
		}
		Type type = typeof(ActModel).Assembly.GetType(text2);
		if (type == null)
		{
			return null;
		}
		object? obj = GenericActMethod.MakeGenericMethod(type).Invoke(null, null);
		return (ActModel?)((obj is ActModel) ? obj : null);
	}

	private static List<ActModel> InvokeGetRandomActList(string seed, Rng rng, UnlockState unlockState, bool isMultiplayer)
	{
		if (GetRandomActListMethod == null)
		{
			throw new InvalidOperationException("ActModel.GetRandomList method was not found.");
		}
		return ((IEnumerable<ActModel>)((GetRandomActListMethod.GetParameters()[0].ParameterType == typeof(string)) ? GetRandomActListMethod.Invoke(null, new object[3] { seed, unlockState, isMultiplayer }) : GetRandomActListMethod.Invoke(null, new object[3] { rng, unlockState, isMultiplayer }))).ToList();
	}
}
