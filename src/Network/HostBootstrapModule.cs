using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.DailyRun;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using RemoveMultiplayerPlayerLimit.Core;
using RemoveMultiplayerPlayerLimit.Infrastructure;

namespace RemoveMultiplayerPlayerLimit.Network;

public partial class HostBootstrapModule : IRMPModule
{
	private partial class HostBootstrapNode : Node
	{



		private readonly HashSet<ulong> _patchedMainMenuSubmenus = new HashSet<ulong>();

		private readonly HashSet<ulong> _patchedHostSubmenus = new HashSet<ulong>();

		private int _frameCounter;

		public HostBootstrapNode()
		{
			((Node)this).Name = new StringName("HostBootstrapNode");
		}

		public override void _Process(double delta)
		{
			if (++_frameCounter % 2 == 0)
			{
				PatchMainMenuSubmenu(SceneMonitor.FindMultiplayerSubmenu());
				PatchHostSubmenu(SceneMonitor.FindMultiplayerHostSubmenu());
			}
		}

		private void PatchMainMenuSubmenu(NMultiplayerSubmenu? submenu)
		{
			if (submenu == null)
			{
				return;
			}
			ulong instanceId = ((GodotObject)submenu).GetInstanceId();
			if (!_patchedMainMenuSubmenus.Contains(instanceId))
			{
				NButton nodeOrNull = ((Node)submenu).GetNodeOrNull<NButton>(new NodePath("ButtonContainer/HostButton"));
				NButton nodeOrNull2 = ((Node)submenu).GetNodeOrNull<NButton>(new NodePath("ButtonContainer/LoadButton"));
				if (nodeOrNull != null && nodeOrNull2 != null && ReplaceReleasedHandler(nodeOrNull, submenu, "OnHostPressed", delegate
				{
					OnMainMenuHostPressed(submenu);
				}) && ReplaceReleasedHandler(nodeOrNull2, submenu, "StartLoad", delegate
				{
					OnMainMenuLoadPressed(submenu);
				}))
				{
					_patchedMainMenuSubmenus.Add(instanceId);
					Log.Info("[RMP:HostBootstrap] Patched NMultiplayerSubmenu host/load handlers.", 2);
				}
			}
		}

		private void PatchHostSubmenu(NMultiplayerHostSubmenu? submenu)
		{
			if (submenu == null)
			{
				return;
			}
			ulong instanceId = ((GodotObject)submenu).GetInstanceId();
			if (!_patchedHostSubmenus.Contains(instanceId))
			{
				NButton nodeOrNull = ((Node)submenu).GetNodeOrNull<NButton>(new NodePath("StandardButton"));
				NButton nodeOrNull2 = ((Node)submenu).GetNodeOrNull<NButton>(new NodePath("DailyButton"));
				NButton nodeOrNull3 = ((Node)submenu).GetNodeOrNull<NButton>(new NodePath("CustomRunButton"));
				if (nodeOrNull != null && nodeOrNull2 != null && nodeOrNull3 != null && ReplaceReleasedHandler(nodeOrNull, submenu, "OnStandardPressed", delegate
				{
					OnHostSubmenuPressed(submenu, (GameMode)1);
				}) && ReplaceReleasedHandler(nodeOrNull2, submenu, "OnDailyPressed", delegate
				{
					OnHostSubmenuPressed(submenu, (GameMode)2);
				}) && ReplaceReleasedHandler(nodeOrNull3, submenu, "OnCustomPressed", delegate
				{
					OnHostSubmenuPressed(submenu, (GameMode)3);
				}))
				{
					_patchedHostSubmenus.Add(instanceId);
					Log.Info("[RMP:HostBootstrap] Patched NMultiplayerHostSubmenu handlers.", 2);
				}
			}
		}

		private void OnMainMenuHostPressed(NMultiplayerSubmenu submenu)
		{
			NSubmenuStack ancestorOfType = NodeUtil.GetAncestorOfType<NSubmenuStack>((Node)(object)submenu);
			if (ancestorOfType == null)
			{
				Log.Warn("[RMP:HostBootstrap] Failed to locate submenu stack for multiplayer submenu.", 2);
				return;
			}
			if (SaveManager.Instance.Progress.NumberOfRuns > 0)
			{
				NMultiplayerHostSubmenu submenuType = ancestorOfType.GetSubmenuType<NMultiplayerHostSubmenu>();
				PatchHostSubmenu(submenuType);
				ancestorOfType.Push((NSubmenu)(object)submenuType);
				return;
			}
			Control nodeOrNull = ((Node)submenu).GetNodeOrNull<Control>(new NodePath("%LoadingOverlay"));
			if (nodeOrNull == null)
			{
				Log.Warn("[RMP:HostBootstrap] Multiplayer submenu loading overlay not found.", 2);
			}
			else
			{
				TaskHelper.RunSafely(StartNewRunHostAsync((GameMode)1, nodeOrNull, ancestorOfType, 16));
			}
		}

		private void OnMainMenuLoadPressed(NMultiplayerSubmenu submenu)
		{
			NSubmenuStack ancestorOfType = NodeUtil.GetAncestorOfType<NSubmenuStack>((Node)(object)submenu);
			Control nodeOrNull = ((Node)submenu).GetNodeOrNull<Control>(new NodePath("%LoadingOverlay"));
			NButton nodeOrNull2 = ((Node)submenu).GetNodeOrNull<NButton>(new NodePath("ButtonContainer/LoadButton"));
			if (ancestorOfType == null || nodeOrNull == null)
			{
				Log.Warn("[RMP:HostBootstrap] Failed to resolve load-run host prerequisites.", 2);
				return;
			}
			PlatformType preferredHostPlatform = GetPreferredHostPlatform();
			ReadSaveResult<SerializableRun> val = SaveManager.Instance.LoadAndCanonicalizeMultiplayerRunSave(PlatformUtil.GetLocalPlayerId(preferredHostPlatform));
			if (!val.Success || val.SaveData == null)
			{
				Log.Warn("[RMP:HostBootstrap] Invalid multiplayer run save detected.", 2);
				if (nodeOrNull2 != null)
				{
					((NClickableControl)nodeOrNull2).Disable();
				}
				NErrorPopup val2 = NErrorPopup.Create(new LocString("main_menu_ui", "INVALID_SAVE_POPUP.title"), new LocString("main_menu_ui", "INVALID_SAVE_POPUP.description_run"), new LocString("main_menu_ui", "INVALID_SAVE_POPUP.dismiss"), true);
				if (val2 != null && NModalContainer.Instance != null)
				{
					NModalContainer.Instance.Add((Node)(object)val2, true);
					NModalContainer.Instance.ShowBackstop();
				}
			}
			else
			{
				TaskHelper.RunSafely(StartLoadedRunHostAsync(val.SaveData, nodeOrNull, ancestorOfType, 16));
			}
		}

		private void OnHostSubmenuPressed(NMultiplayerHostSubmenu submenu, GameMode gameMode)
		{
			NSubmenuStack ancestorOfType = NodeUtil.GetAncestorOfType<NSubmenuStack>((Node)(object)submenu);
			Control nodeOrNull = ((Node)submenu).GetNodeOrNull<Control>(new NodePath("%LoadingOverlay"));
			if (ancestorOfType == null || nodeOrNull == null)
			{
				Log.Warn("[RMP:HostBootstrap] Failed to resolve multiplayer host submenu prerequisites.", 2);
			}
			else
			{
				TaskHelper.RunSafely(StartNewRunHostAsync(gameMode, nodeOrNull, ancestorOfType, 16));
			}
		}

		private static bool ReplaceReleasedHandler(NButton button, object signalTarget, string originalMethodName, Action<NButton> replacement)
		{
			try
			{
				Callable val = CreatePrivateReleasedCallable(signalTarget, originalMethodName);
				if (((GodotObject)button).IsConnected(NClickableControl.SignalName.Released, val))
				{
					((GodotObject)button).Disconnect(NClickableControl.SignalName.Released, val);
				}
				((GodotObject)button).Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(replacement), 0u);
				return true;
			}
			catch (Exception ex)
			{
				Log.Warn($"[RMP:HostBootstrap] Failed to rewire {signalTarget.GetType().Name}.{originalMethodName}: {ex.Message}", 2);
				return false;
			}
		}

		private static Callable CreatePrivateReleasedCallable(object target, string methodName)
		{
			MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				throw new MissingMethodException(target.GetType().FullName, methodName);
			}
			return Callable.From<NButton>((Action<NButton>)Delegate.CreateDelegate(typeof(Action<NButton>), target, method));
		}

		private static async Task StartNewRunHostAsync(GameMode gameMode, Control loadingOverlay, NSubmenuStack stack, int hostCapacity)
		{
			((CanvasItem)loadingOverlay).Visible = true;
			try
			{
				NetHostGameService netService = new NetHostGameService();
				NetErrorInfo? val = await StartHostAsync(netService, hostCapacity);
				if (val.HasValue)
				{
					ShowNetError(val.Value);
					return;
				}
				TrackHostCapacity((INetGameService)(object)netService, hostCapacity);
				if ((int)gameMode != 1)
				{
					if ((int)gameMode == 2)
					{
						NDailyRunScreen submenuType = stack.GetSubmenuType<NDailyRunScreen>();
						submenuType.InitializeMultiplayerAsHost((INetGameService)(object)netService);
						stack.Push((NSubmenu)(object)submenuType);
					}
					else
					{
						NCustomRunScreen submenuType2 = stack.GetSubmenuType<NCustomRunScreen>();
						submenuType2.InitializeMultiplayerAsHost((INetGameService)(object)netService, hostCapacity);
						stack.Push((NSubmenu)(object)submenuType2);
					}
				}
				else
				{
					NCharacterSelectScreen submenuType3 = stack.GetSubmenuType<NCharacterSelectScreen>();
					submenuType3.InitializeMultiplayerAsHost((INetGameService)(object)netService, hostCapacity);
					stack.Push((NSubmenu)(object)submenuType3);
				}
				Log.Info($"[RMP:HostBootstrap] Hosted {gameMode} lobby via {GetTransportName((INetGameService)(object)netService)} with capacity {hostCapacity}.", 2);
			}
			catch (Exception value)
			{
				ShowNetError(new NetErrorInfo((NetError)17, false));
				Log.Warn($"[RMP:HostBootstrap] New-run host startup failed: {value}", 2);
				throw;
			}
			finally
			{
				((CanvasItem)loadingOverlay).Visible = false;
			}
		}

		private static async Task StartLoadedRunHostAsync(SerializableRun run, Control loadingOverlay, NSubmenuStack stack, int hostCapacity)
		{
			((CanvasItem)loadingOverlay).Visible = true;
			try
			{
				NetHostGameService netService = new NetHostGameService();
				NetErrorInfo? val = await StartHostAsync(netService, hostCapacity);
				if (val.HasValue)
				{
					ShowNetError(val.Value);
					return;
				}
				TrackHostCapacity((INetGameService)(object)netService, hostCapacity);
				GameMode val2 = ResolveGameMode(run);
				if ((int)val2 != 2)
				{
					if ((int)val2 == 3)
					{
						NCustomRunLoadScreen submenuType = stack.GetSubmenuType<NCustomRunLoadScreen>();
						submenuType.InitializeAsHost((INetGameService)(object)netService, run);
						stack.Push((NSubmenu)(object)submenuType);
					}
					else
					{
						NMultiplayerLoadGameScreen submenuType2 = stack.GetSubmenuType<NMultiplayerLoadGameScreen>();
						submenuType2.InitializeAsHost((INetGameService)(object)netService, run);
						stack.Push((NSubmenu)(object)submenuType2);
					}
				}
				else
				{
					NDailyRunLoadScreen submenuType3 = stack.GetSubmenuType<NDailyRunLoadScreen>();
					submenuType3.InitializeAsHost((INetGameService)(object)netService, run);
					stack.Push((NSubmenu)(object)submenuType3);
				}
				Log.Info($"[RMP:HostBootstrap] Hosted loaded {val2} lobby via {GetTransportName((INetGameService)(object)netService)} with capacity {hostCapacity}, savePlayers={run.Players.Count}, connectedPlayers=1.", 2);
			}
			catch (Exception value)
			{
				ShowNetError(new NetErrorInfo((NetError)17, false));
				Log.Warn($"[RMP:HostBootstrap] Loaded-run host startup failed: {value}", 2);
				throw;
			}
			finally
			{
				((CanvasItem)loadingOverlay).Visible = false;
			}
		}

		private static async Task<NetErrorInfo?> StartHostAsync(NetHostGameService netService, int hostCapacity)
		{
			if ((int)GetPreferredHostPlatform() == 1)
			{
				return await netService.StartSteamHost(hostCapacity);
			}
			return netService.StartENetHost((ushort)33771, hostCapacity);
		}

		private static PlatformType GetPreferredHostPlatform()
		{
			if (SteamInitializer.Initialized && !CommandLineHelper.HasArg("fastmp"))
			{
				return (PlatformType)1;
			}
			return (PlatformType)0;
		}

		private static void TrackHostCapacity(INetGameService netService, int hostCapacity)
		{
			TrackedHostCapacities[netService] = hostCapacity;
		}

		private static void ShowNetError(NetErrorInfo error)
		{
			NErrorPopup val = NErrorPopup.Create(error);
			if (val != null && NModalContainer.Instance != null)
			{
				NModalContainer.Instance.Add((Node)(object)val, true);
			}
		}

		private static GameMode ResolveGameMode(SerializableRun run)
		{
			object obj = SerializableRunGameModeProperty?.GetValue(run);
			if (obj is GameMode)
			{
				return (GameMode)obj;
			}
			obj = SerializableRunGameModeField?.GetValue(run);
			if (obj is GameMode)
			{
				return (GameMode)obj;
			}
			if (!run.DailyTime.HasValue)
			{
				if (run.Modifiers.Count > 0)
				{
					return (GameMode)3;
				}
				return (GameMode)1;
			}
			return (GameMode)2;
		}









	}

	private const ushort DefaultEnetPort = 33771;

	private static readonly Dictionary<INetGameService, int> TrackedHostCapacities = new Dictionary<INetGameService, int>();

	private static readonly PropertyInfo? SerializableRunGameModeProperty = typeof(SerializableRun).GetProperty("GameMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

	private static readonly FieldInfo? SerializableRunGameModeField = typeof(SerializableRun).GetField("GameMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? typeof(SerializableRun).GetField("gameMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

	private bool _deferToDcip;

	public string Name => "HostBootstrap";

	public void Initialize(ConfigManager config, ReflectionCache cache)
	{
		if (IsDirectConnectIpLoaded())
		{
			_deferToDcip = true;
			Log.Warn("[RMP:HostBootstrap] DirectConnectIP detected — RMP is yielding multiplayer host bootstrap to DCIP to avoid transport protocol conflicts (DCIP replaces NetHostGameService with its own DirectHost/DirectClient). While DCIP is loaded, RMP's 4-16 player slider will NOT affect host capacity: Steam mode stays at vanilla 4, Direct-IP mode is capped at DCIP's hardcoded 16. Remove one of the two mods to regain full control.", 2);
		}
	}

	public Node? CreateNode()
	{
		if (!_deferToDcip)
		{
			return new HostBootstrapNode();
		}
		return null;
	}

	private static bool IsDirectConnectIpLoaded()
	{
		try
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for (int i = 0; i < assemblies.Length; i++)
			{
				if (string.Equals(assemblies[i].GetName().Name, "DirectConnectIP", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	public void Cleanup()
	{
		TrackedHostCapacities.Clear();
	}

	internal static bool TryGetTrackedHostCapacity(INetGameService? netService, out int capacity)
	{
		if (netService != null && TrackedHostCapacities.TryGetValue(netService, out capacity))
		{
			return true;
		}
		capacity = 0;
		return false;
	}

	internal static string GetTransportName(INetGameService netService)
	{
		if ((int)netService.Platform != 1)
		{
			return "ENet";
		}
		return "Steam";
	}
}
