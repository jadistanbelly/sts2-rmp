using System;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Runs;
using RemoveMultiplayerPlayerLimit.Core;

namespace RemoveMultiplayerPlayerLimit.Infrastructure;

public static class GameStateAccessor
{
	private static readonly PropertyInfo? RunManagerStateProperty = typeof(RunManager).GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic);

	public static int GetPlayerCount()
	{
		try
		{
			RunManager instance = RunManager.Instance;
			if (instance == null)
			{
				return 1;
			}
			object? obj = RunManagerStateProperty?.GetValue(instance);
			object? obj2 = ((obj is RunState) ? obj : null);
			return ((obj2 == null) ? ((int?)null) : ((RunState)obj2).Players?.Count) ?? 1;
		}
		catch
		{
			return 1;
		}
	}

	public static RunState? GetRunState()
	{
		try
		{
			RunManager instance = RunManager.Instance;
			if (instance == null)
			{
				return null;
			}
			object? obj = RunManagerStateProperty?.GetValue(instance);
			return (RunState?)((obj is RunState) ? obj : null);
		}
		catch
		{
			return null;
		}
	}

	public static bool IsMultiplayer()
	{
		try
		{
			MultiplayerApi multiplayer = ((SceneTree)Engine.GetMainLoop()).GetMultiplayer((NodePath)null);
			return multiplayer == null || multiplayer.GetUniqueId() != 0;
		}
		catch
		{
			return false;
		}
	}

	public static bool IsServer()
	{
		try
		{
			MultiplayerApi multiplayer = ((SceneTree)Engine.GetMainLoop()).GetMultiplayer((NodePath)null);
			return multiplayer != null && multiplayer.IsServer();
		}
		catch
		{
			return false;
		}
	}

	public static int GetEffectivePlayerCount(int rawCount)
	{
		if (!ProtocolConfig.DifficultyScalingEnabled)
		{
			return Math.Min(rawCount, 4);
		}
		return rawCount;
	}
}
