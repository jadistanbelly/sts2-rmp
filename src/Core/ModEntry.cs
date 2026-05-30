using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using RemoveMultiplayerPlayerLimit.Features.CampfireLayout;
using RemoveMultiplayerPlayerLimit.Features.DifficultyScaling;
using RemoveMultiplayerPlayerLimit.Features.SettingsUI;
using RemoveMultiplayerPlayerLimit.Features.ShopLayout;
using RemoveMultiplayerPlayerLimit.Features.TreasureRoom;
using RemoveMultiplayerPlayerLimit.Features.VictoryFlow;
using RemoveMultiplayerPlayerLimit.Infrastructure;
using RemoveMultiplayerPlayerLimit.Network;
using RemoveMultiplayerPlayerLimit.Platform;

namespace RemoveMultiplayerPlayerLimit.Core;

[ModInitializer("Initialize")]
public static class ModEntry
{
	internal const int VanillaMultiplayerHolderCount = 4;

	private static readonly List<IRMPModule> Modules = new List<IRMPModule>();

	private static Node? _root;

	public static void Initialize()
	{
		Log.Warn("[RMP] Initializing v0.1.8...", 2);
		Modules.Clear();
		ConfigManager configManager = new ConfigManager();
		ReflectionCache cache = new ReflectionCache();
		int value = 16;
		int value2 = 32;
		Modules.Add(new DifficultyModule());
		Modules.Add(new CampfireModule());
		Modules.Add(new ShopModule());
		Modules.Add(new TreasureModule());
		Modules.Add(new SettingsModule());
		Modules.Add(new VictoryModule());
		Modules.Add(new HostBootstrapModule());
		Modules.Add(new ExtendedLobbyModule());
		Modules.Add(new LobbyManagerModule());
		if (PlatformDetector.IsMacOS)
		{
			Modules.Add(new MacOsTlsModule());
		}
		foreach (IRMPModule module in Modules)
		{
			try
			{
				module.Initialize(configManager, cache);
			}
			catch (Exception value3)
			{
				Log.Warn($"[RMP] Failed to initialize module {module.Name}: {value3}", 2);
			}
		}
		SceneTree val = (SceneTree)Engine.GetMainLoop();
		_root = new Node
		{
			Name = new StringName("RMPController")
		};
		_root.AddChild(SceneMonitor.CreateRegistryNode(), false, Node.InternalMode.Disabled);
		foreach (IRMPModule module2 in Modules)
		{
			try
			{
				Node val2 = module2.CreateNode();
				if (val2 != null)
				{
					_root.AddChild(val2, false, Node.InternalMode.Disabled);
				}
			}
			catch (Exception value4)
			{
				Log.Warn($"[RMP] Failed to create node for module {module2.Name}: {value4}", 2);
			}
		}
		((GodotObject)val.Root).CallDeferred(new StringName("add_child"), (Variant[])(object)new Variant[1] { Variant.From(_root) });
		Log.Warn($"[RMP] All modules loaded. Player limit fixed at {16}, slot bits: {4} (cap {value}), lobby bits: {5} (cap {value2}), difficulty scaling: {ProtocolConfig.DifficultyScalingEnabled}, macOS TLS: {configManager.MacOsTlsWorkaround}", 2);
	}
}
