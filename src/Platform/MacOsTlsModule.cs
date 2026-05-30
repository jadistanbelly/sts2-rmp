using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using RemoveMultiplayerPlayerLimit.Core;
using RemoveMultiplayerPlayerLimit.Infrastructure;

namespace RemoveMultiplayerPlayerLimit.Platform;

public partial class MacOsTlsModule : IRMPModule
{
	private partial class MacOsTlsNode : Node
	{



		private readonly MacOsTlsModule _mod;

		private int _frameCounter;

		private bool _wasConnecting;

		public MacOsTlsNode(MacOsTlsModule mod)
		{
			_mod = mod;
			((Node)this).Name = new StringName("MacOsTlsNode");
		}

		public override void _Process(double delta)
		{
			if (!_mod._config.MacOsTlsWorkaround || ++_frameCounter % 60 != 0)
			{
				return;
			}
			try
			{
				MultiplayerApi multiplayer = ((SceneTree)Engine.GetMainLoop()).GetMultiplayer((NodePath)null);
				MultiplayerPeer obj = ((multiplayer != null) ? multiplayer.MultiplayerPeer : null);
				bool flag = obj != null && (long)obj.GetConnectionStatus() == 1;
				if (flag && !_wasConnecting && !_mod._workaroundLogged)
				{
					Log.Warn("[RMP:TLS] Multiplayer connection detected — TLS workaround is active.", 2);
					_mod._workaroundLogged = true;
				}
				_wasConnecting = flag;
			}
			catch
			{
			}
		}








	}

	private ConfigManager _config;

	private ReflectionCache _cache;

	private bool _workaroundLogged;

	public string Name => "MacOSTls";

	public void Initialize(ConfigManager config, ReflectionCache cache)
	{
		_config = config;
		_cache = cache;
		if (config.MacOsTlsWorkaround)
		{
			ApplyEnvironmentWorkaround();
		}
	}

	public Node? CreateNode()
	{
		return (Node?)(object)new MacOsTlsNode(this);
	}

	public void Cleanup()
	{
	}

	private void ApplyEnvironmentWorkaround()
	{
		try
		{
			if (!_workaroundLogged)
			{
				Log.Warn("[RMP:TLS] macOS TLS workaround active — multiplayer cert validation may be relaxed.", 2);
				_workaroundLogged = true;
			}
		}
		catch (Exception ex)
		{
			Log.Warn("[RMP:TLS] Environment workaround failed: " + ex.Message, 2);
		}
	}

	internal static TlsOptions? CreateUnsafeTlsOptions(X509Certificate? trustedChain)
	{
		try
		{
			MethodInfo method = typeof(TlsOptions).GetMethod("ClientUnsafe", BindingFlags.Static | BindingFlags.Public, null, new Type[1] { typeof(X509Certificate) }, null);
			if (method != null)
			{
				return (TlsOptions)method.Invoke(null, new object[1] { trustedChain });
			}
			MethodInfo method2 = typeof(TlsOptions).GetMethod("ClientUnsafe", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
			if (method2 != null)
			{
				return (TlsOptions)method2.Invoke(null, Array.Empty<object>());
			}
		}
		catch (Exception ex)
		{
			Log.Warn("[RMP:TLS] Failed to create unsafe TLS options: " + ex.Message, 2);
		}
		return null;
	}

	internal static bool IsMultiplayerContext()
	{
		return (new StackTrace(fNeedFileInfo: false).GetFrames() ?? Array.Empty<StackFrame>()).Any(delegate(StackFrame f)
		{
			string text = f.GetMethod()?.DeclaringType?.FullName;
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}
			return text.StartsWith("MegaCrit.Sts2.Core.Multiplayer.", StringComparison.Ordinal) || text.StartsWith("MegaCrit.Sts2.Core.Platform.Steam.SteamJoinCallbackHandler", StringComparison.Ordinal) || text.StartsWith("MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NJoinFriendScreen", StringComparison.Ordinal) || text.StartsWith("MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NMultiplayer", StringComparison.Ordinal) || text.StartsWith("MegaCrit.Sts2.Core.Nodes.Debug.Multiplayer.", StringComparison.Ordinal);
		});
	}
}
