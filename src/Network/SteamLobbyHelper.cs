using System;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Multiplayer.Transport.Steam;
using Steamworks;

namespace RemoveMultiplayerPlayerLimit.Network;

internal static class SteamLobbyHelper
{
	internal static bool TryUpdateMemberLimit(INetGameService netService, int limit)
	{
		try
		{
			NetHostGameService val = (NetHostGameService)(object)((netService is NetHostGameService) ? netService : null);
			if (val == null)
			{
				return false;
			}
			NetHost netHost = val.NetHost;
			SteamHost val2 = (SteamHost)(object)((netHost is SteamHost) ? netHost : null);
			if (val2 == null)
			{
				return false;
			}
			CSteamID? lobbyId = val2.LobbyId;
			if (!lobbyId.HasValue)
			{
				return false;
			}
			bool num = SteamMatchmaking.SetLobbyMemberLimit(lobbyId.Value, limit);
			if (num)
			{
				Log.Info($"[RMP] Steam lobby member limit set to {limit} (lobby={lobbyId.Value.m_SteamID})", 2);
			}
			else
			{
				Log.Warn($"[RMP] SteamMatchmaking.SetLobbyMemberLimit({limit}) returned false", 2);
			}
			return num;
		}
		catch (Exception ex)
		{
			Log.Warn("[RMP] Failed to update Steam lobby limit: " + ex.Message, 2);
			return false;
		}
	}

	internal static int GetCurrentMemberLimit(INetGameService netService)
	{
		try
		{
			NetHostGameService val = (NetHostGameService)(object)((netService is NetHostGameService) ? netService : null);
			if (val == null)
			{
				return -1;
			}
			NetHost netHost = val.NetHost;
			SteamHost val2 = (SteamHost)(object)((netHost is SteamHost) ? netHost : null);
			if (val2 == null)
			{
				return -1;
			}
			CSteamID? lobbyId = val2.LobbyId;
			if (!lobbyId.HasValue)
			{
				return -1;
			}
			return SteamMatchmaking.GetLobbyMemberLimit(lobbyId.Value);
		}
		catch
		{
			return -1;
		}
	}
}
