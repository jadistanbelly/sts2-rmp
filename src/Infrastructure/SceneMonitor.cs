using System;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.DailyRun;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;

namespace RemoveMultiplayerPlayerLimit.Infrastructure;

public static class SceneMonitor
{
	private static readonly FieldInfo? DailyRunLobbyField = typeof(NDailyRunScreen).GetField("_lobby", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo? MultiplayerLoadLobbyField = typeof(NMultiplayerLoadGameScreen).GetField("_runLobby", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo? CustomRunLoadLobbyField = typeof(NCustomRunLoadScreen).GetField("_lobby", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo? DailyRunLoadLobbyField = typeof(NDailyRunLoadScreen).GetField("_lobby", BindingFlags.Instance | BindingFlags.NonPublic);

	public static Node CreateRegistryNode()
	{
		return (Node)(object)new SceneRegistry();
	}

	public static string GetCurrentSceneName()
	{
		Node currentScene = ((SceneTree)Engine.GetMainLoop()).CurrentScene;
		return ((currentScene != null) ? ((object)currentScene.Name).ToString() : null) ?? string.Empty;
	}

	public static bool IsSceneActive(string nameContains)
	{
		Node currentScene = ((SceneTree)Engine.GetMainLoop()).CurrentScene;
		if (currentScene == null)
		{
			return false;
		}
		return ((object)currentScene.Name).ToString().Contains(nameContains, StringComparison.OrdinalIgnoreCase);
	}

	public static Node GetRoot()
	{
		return (Node)(object)((SceneTree)Engine.GetMainLoop()).Root;
	}

	public static NSettingsScreen? FindSettingsScreen()
	{
		NSettingsScreen val = SceneRegistry.Instance?.SettingsScreen;
		if (val == null || !((CanvasItem)val).IsVisibleInTree())
		{
			return null;
		}
		return val;
	}

	public static NRestSiteRoom? FindRestSiteRoom()
	{
		return SceneRegistry.Instance?.RestSiteRoom;
	}

	public static NMerchantRoom? FindMerchantRoom()
	{
		return SceneRegistry.Instance?.MerchantRoom;
	}

	public static NTreasureRoomRelicCollection? FindTreasureRoomRelicCollection()
	{
		return SceneRegistry.Instance?.TreasureRoomRelicCollection;
	}

	public static NMultiplayerSubmenu? FindMultiplayerSubmenu()
	{
		return SceneRegistry.Instance?.MultiplayerSubmenu;
	}

	public static NMultiplayerHostSubmenu? FindMultiplayerHostSubmenu()
	{
		return SceneRegistry.Instance?.MultiplayerHostSubmenu;
	}

	public static NMultiplayerLoadGameScreen? FindMultiplayerLoadGameScreen()
	{
		return SceneRegistry.Instance?.MultiplayerLoadGameScreen;
	}

	public static NCustomRunLoadScreen? FindCustomRunLoadScreen()
	{
		return SceneRegistry.Instance?.CustomRunLoadScreen;
	}

	public static NDailyRunLoadScreen? FindDailyRunLoadScreen()
	{
		return SceneRegistry.Instance?.DailyRunLoadScreen;
	}

	public static Node? FindNodeByName(Node root, string nameContains)
	{
		if (((object)root.Name).ToString().Contains(nameContains, StringComparison.Ordinal))
		{
			return root;
		}
		foreach (Node child in root.GetChildren(false))
		{
			Node val = FindNodeByName(child, nameContains);
			if (val != null)
			{
				return val;
			}
		}
		return null;
	}

	public static T? FindNodeOfType<T>(Node root) where T : Node
	{
		if (root == GetRoot() && TryGetCachedNode<T>(out T node))
		{
			return node;
		}
		T val = (T)(object)((root is T) ? root : null);
		if (val != null)
		{
			return val;
		}
		foreach (Node child in root.GetChildren(false))
		{
			T val2 = FindNodeOfType<T>(child);
			if (val2 != null)
			{
				return val2;
			}
		}
		return default(T);
	}

	public static Node? FindNode(Node root, Func<Node, bool> predicate)
	{
		if (predicate(root))
		{
			return root;
		}
		foreach (Node child in root.GetChildren(false))
		{
			Node val = FindNode(child, predicate);
			if (val != null)
			{
				return val;
			}
		}
		return null;
	}

	public static StartRunLobby? FindActiveStartRunLobby()
	{
		try
		{
			NCharacterSelectScreen val = SceneRegistry.Instance?.CharacterSelectScreen;
			if (((val != null) ? val.Lobby : null) != null)
			{
				return val.Lobby;
			}
			NCustomRunScreen val2 = SceneRegistry.Instance?.CustomRunScreen;
			if (((val2 != null) ? val2.Lobby : null) != null)
			{
				return val2.Lobby;
			}
			object? obj = DailyRunLobbyField?.GetValue(SceneRegistry.Instance?.DailyRunScreen);
			StartRunLobby val3 = (StartRunLobby)((obj is StartRunLobby) ? obj : null);
			if (val3 != null)
			{
				return val3;
			}
			Node root = GetRoot();
			val = SceneMonitor.FindNodeOfType<NCharacterSelectScreen>(root);
			if (((val != null) ? val.Lobby : null) != null)
			{
				return val.Lobby;
			}
			val2 = SceneMonitor.FindNodeOfType<NCustomRunScreen>(root);
			if (((val2 != null) ? val2.Lobby : null) != null)
			{
				return val2.Lobby;
			}
			if (DailyRunLobbyField != null)
			{
				Node val4 = FindNode(root, (Node n) => ((object)n).GetType().FullName == "MegaCrit.Sts2.Core.Nodes.Screens.DailyRun.NDailyRunScreen");
				if (val4 != null)
				{
					object? value = DailyRunLobbyField.GetValue(val4);
					StartRunLobby val5 = (StartRunLobby)((value is StartRunLobby) ? value : null);
					if (val5 != null)
					{
						return val5;
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public static LoadRunLobby? FindActiveLoadRunLobby()
	{
		try
		{
			object? obj = MultiplayerLoadLobbyField?.GetValue(SceneRegistry.Instance?.MultiplayerLoadGameScreen);
			LoadRunLobby val = (LoadRunLobby)((obj is LoadRunLobby) ? obj : null);
			if (val != null)
			{
				return val;
			}
			object? obj2 = CustomRunLoadLobbyField?.GetValue(SceneRegistry.Instance?.CustomRunLoadScreen);
			LoadRunLobby val2 = (LoadRunLobby)((obj2 is LoadRunLobby) ? obj2 : null);
			if (val2 != null)
			{
				return val2;
			}
			object? obj3 = DailyRunLoadLobbyField?.GetValue(SceneRegistry.Instance?.DailyRunLoadScreen);
			LoadRunLobby val3 = (LoadRunLobby)((obj3 is LoadRunLobby) ? obj3 : null);
			if (val3 != null)
			{
				return val3;
			}
			Node root = GetRoot();
			NMultiplayerLoadGameScreen val4 = SceneMonitor.FindNodeOfType<NMultiplayerLoadGameScreen>(root);
			if (val4 != null)
			{
				object? obj4 = MultiplayerLoadLobbyField?.GetValue(val4);
				LoadRunLobby val5 = (LoadRunLobby)((obj4 is LoadRunLobby) ? obj4 : null);
				if (val5 != null)
				{
					return val5;
				}
			}
			NCustomRunLoadScreen val6 = SceneMonitor.FindNodeOfType<NCustomRunLoadScreen>(root);
			if (val6 != null)
			{
				object? obj5 = CustomRunLoadLobbyField?.GetValue(val6);
				LoadRunLobby val7 = (LoadRunLobby)((obj5 is LoadRunLobby) ? obj5 : null);
				if (val7 != null)
				{
					return val7;
				}
			}
			NDailyRunLoadScreen val8 = SceneMonitor.FindNodeOfType<NDailyRunLoadScreen>(root);
			if (val8 != null)
			{
				object? obj6 = DailyRunLoadLobbyField?.GetValue(val8);
				LoadRunLobby val9 = (LoadRunLobby)((obj6 is LoadRunLobby) ? obj6 : null);
				if (val9 != null)
				{
					return val9;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public static string? GetActiveLoadLobbyScreenName()
	{
		if (MultiplayerLoadLobbyField?.GetValue(SceneRegistry.Instance?.MultiplayerLoadGameScreen) is LoadRunLobby)
		{
			return "NMultiplayerLoadGameScreen";
		}
		if (CustomRunLoadLobbyField?.GetValue(SceneRegistry.Instance?.CustomRunLoadScreen) is LoadRunLobby)
		{
			return "NCustomRunLoadScreen";
		}
		if (DailyRunLoadLobbyField?.GetValue(SceneRegistry.Instance?.DailyRunLoadScreen) is LoadRunLobby)
		{
			return "NDailyRunLoadScreen";
		}
		return null;
	}

	private static bool TryGetCachedNode<T>(out T? node) where T : Node
	{
		SceneRegistry instance = SceneRegistry.Instance;
		T val;
		if (instance == null)
		{
			val = default(T);
		}
		else if (typeof(T) == typeof(NSettingsScreen))
		{
			NSettingsScreen? settingsScreen = instance.SettingsScreen;
			val = (T)(object)((settingsScreen is T) ? settingsScreen : null);
		}
		else if (typeof(T) == typeof(NRestSiteRoom))
		{
			NRestSiteRoom? restSiteRoom = instance.RestSiteRoom;
			val = (T)(object)((restSiteRoom is T) ? restSiteRoom : null);
		}
		else if (typeof(T) == typeof(NMerchantRoom))
		{
			NMerchantRoom? merchantRoom = instance.MerchantRoom;
			val = (T)(object)((merchantRoom is T) ? merchantRoom : null);
		}
		else if (typeof(T) == typeof(NTreasureRoomRelicCollection))
		{
			NTreasureRoomRelicCollection? treasureRoomRelicCollection = instance.TreasureRoomRelicCollection;
			val = (T)(object)((treasureRoomRelicCollection is T) ? treasureRoomRelicCollection : null);
		}
		else if (typeof(T) == typeof(NCharacterSelectScreen))
		{
			NCharacterSelectScreen? characterSelectScreen = instance.CharacterSelectScreen;
			val = (T)(object)((characterSelectScreen is T) ? characterSelectScreen : null);
		}
		else if (typeof(T) == typeof(NCustomRunScreen))
		{
			NCustomRunScreen? customRunScreen = instance.CustomRunScreen;
			val = (T)(object)((customRunScreen is T) ? customRunScreen : null);
		}
		else if (typeof(T) == typeof(NDailyRunScreen))
		{
			NDailyRunScreen? dailyRunScreen = instance.DailyRunScreen;
			val = (T)(object)((dailyRunScreen is T) ? dailyRunScreen : null);
		}
		else if (typeof(T) == typeof(NMultiplayerLoadGameScreen))
		{
			NMultiplayerLoadGameScreen? multiplayerLoadGameScreen = instance.MultiplayerLoadGameScreen;
			val = (T)(object)((multiplayerLoadGameScreen is T) ? multiplayerLoadGameScreen : null);
		}
		else if (typeof(T) == typeof(NCustomRunLoadScreen))
		{
			NCustomRunLoadScreen? customRunLoadScreen = instance.CustomRunLoadScreen;
			val = (T)(object)((customRunLoadScreen is T) ? customRunLoadScreen : null);
		}
		else if (typeof(T) == typeof(NDailyRunLoadScreen))
		{
			NDailyRunLoadScreen? dailyRunLoadScreen = instance.DailyRunLoadScreen;
			val = (T)(object)((dailyRunLoadScreen is T) ? dailyRunLoadScreen : null);
		}
		else if (typeof(T) == typeof(NMultiplayerSubmenu))
		{
			NMultiplayerSubmenu? multiplayerSubmenu = instance.MultiplayerSubmenu;
			val = (T)(object)((multiplayerSubmenu is T) ? multiplayerSubmenu : null);
		}
		else if (typeof(T) == typeof(NMultiplayerHostSubmenu))
		{
			NMultiplayerHostSubmenu? multiplayerHostSubmenu = instance.MultiplayerHostSubmenu;
			val = (T)(object)((multiplayerHostSubmenu is T) ? multiplayerHostSubmenu : null);
		}
		else
		{
			val = default(T);
		}
		node = val;
		return node != null;
	}
}
