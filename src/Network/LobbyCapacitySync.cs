using System;

namespace RemoveMultiplayerPlayerLimit.Network;

internal static class LobbyCapacitySync
{
	internal static bool TrySyncSteamLimit(Func<int> getCurrentLimit, Func<int, bool> setLimit, int targetLimit)
	{
		int currentLimit = getCurrentLimit();
		if (currentLimit == targetLimit)
		{
			return false;
		}

		return setLimit(targetLimit);
	}
}
