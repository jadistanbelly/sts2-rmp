using System.Collections.Generic;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Singleton;
using RemoveMultiplayerPlayerLimit.Core;
using RemoveMultiplayerPlayerLimit.Infrastructure;

namespace RemoveMultiplayerPlayerLimit.Features.DifficultyScaling;

public partial class DifficultyModule : IRMPModule
{
	private partial class DifficultyNode : Node
	{



		private readonly DifficultyModule _module;

		private bool _wasInCombat;

		private bool _scalingChecked;

		private int _frameCounter;

		public DifficultyNode(DifficultyModule module)
		{
			_module = module;
			((Node)this).Name = new StringName("DifficultyNode");
		}

		public override void _Process(double delta)
		{
			if (++_frameCounter % 10 != 0)
			{
				return;
			}
			bool flag = IsCombatActive();
			if (flag && !_wasInCombat)
			{
				_scalingChecked = false;
			}
			if (flag && !_scalingChecked)
			{
				int playerCount = GameStateAccessor.GetPlayerCount();
				if (playerCount > 4)
				{
					GameStateAccessor.GetEffectivePlayerCount(playerCount);
					if (ProtocolConfig.DifficultyScalingEnabled)
					{
						ReapplyMonsterScaling(playerCount);
					}
				}
				_scalingChecked = true;
			}
			if (!flag && _wasInCombat)
			{
				_scalingChecked = false;
			}
			_wasInCombat = flag;
		}

		private static bool IsCombatActive()
		{
			try
			{
				return SceneMonitor.IsSceneActive("Combat") || SceneMonitor.IsSceneActive("combat");
			}
			catch
			{
				return false;
			}
		}

		private void ReapplyMonsterScaling(int actualPlayerCount)
		{
			Log.Warn($"[RMP:Difficulty] Scaling check: {actualPlayerCount} players, enabled={ProtocolConfig.DifficultyScalingEnabled}", 2);
		}









	}

	private ReflectionCache _cache;

	private FieldInfo? _runStateField;

	public string Name => "DifficultyScaling";

	public void Initialize(ConfigManager config, ReflectionCache cache)
	{
		_cache = cache;
		_runStateField = cache.GetField(typeof(MultiplayerScalingModel), "_runState");
	}

	public Node? CreateNode()
	{
		return (Node?)(object)new DifficultyNode(this);
	}

	public void Cleanup()
	{
	}
}
