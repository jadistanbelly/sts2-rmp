using System;
using RemoveMultiplayerPlayerLimit.Network;

namespace RemoveMultiplayerPlayerLimit.Tests;

internal static class LobbyCapacitySyncTests
{
	private static int Main()
	{
		(string Name, Action Test)[] tests =
		[
			("LoadedRunHostUpdatesSteamLimitWhenCurrentLimitIsVanilla", LoadedRunHostUpdatesSteamLimitWhenCurrentLimitIsVanilla),
			("CurrentTargetLimitDoesNotCallSteam", CurrentTargetLimitDoesNotCallSteam),
			("UnknownCurrentLimitStillAttemptsRepair", UnknownCurrentLimitStillAttemptsRepair)
		];

		int failures = 0;
		foreach ((string name, Action test) in tests)
		{
			try
			{
				test();
				Console.WriteLine($"PASS {name}");
			}
			catch (Exception ex)
			{
				failures++;
				Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
			}
		}

		return failures;
	}

	private static void LoadedRunHostUpdatesSteamLimitWhenCurrentLimitIsVanilla()
	{
		FakeSteamLobby steam = new(currentLimit: 4);

		bool changed = LobbyCapacitySync.TrySyncSteamLimit(steam.GetLimit, steam.SetLimit, 16);

		AssertEx.Equal(true, changed);
		AssertEx.Equal(16, steam.CurrentLimit);
		AssertEx.Equal(1, steam.SetCalls);
	}

	private static void CurrentTargetLimitDoesNotCallSteam()
	{
		FakeSteamLobby steam = new(currentLimit: 16);

		bool changed = LobbyCapacitySync.TrySyncSteamLimit(steam.GetLimit, steam.SetLimit, 16);

		AssertEx.Equal(false, changed);
		AssertEx.Equal(16, steam.CurrentLimit);
		AssertEx.Equal(0, steam.SetCalls);
	}

	private static void UnknownCurrentLimitStillAttemptsRepair()
	{
		FakeSteamLobby steam = new(currentLimit: -1);

		bool changed = LobbyCapacitySync.TrySyncSteamLimit(steam.GetLimit, steam.SetLimit, 16);

		AssertEx.Equal(true, changed);
		AssertEx.Equal(16, steam.CurrentLimit);
		AssertEx.Equal(1, steam.SetCalls);
	}

	private sealed class FakeSteamLobby(int currentLimit)
	{
		public int CurrentLimit { get; private set; } = currentLimit;

		public int SetCalls { get; private set; }

		public int GetLimit()
		{
			return CurrentLimit;
		}

		public bool SetLimit(int limit)
		{
			SetCalls++;
			CurrentLimit = limit;
			return true;
		}
	}
}

internal static class AssertEx
{
	public static void Equal<T>(T expected, T actual)
	{
		if (!Equals(expected, actual))
		{
			throw new InvalidOperationException($"Expected {expected}, got {actual}");
		}
	}
}
