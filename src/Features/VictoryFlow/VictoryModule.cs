using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RemoveMultiplayerPlayerLimit.Core;
using RemoveMultiplayerPlayerLimit.Infrastructure;

namespace RemoveMultiplayerPlayerLimit.Features.VictoryFlow;

public partial class VictoryModule : IRMPModule
{
	private sealed partial class VictoryNode : Node
	{



		private ulong _lastRunNodeId;

		private int _frameCounter;

		private int _missingGameOverTicks;

		private bool _summaryForcedForCurrentRun;

		public VictoryNode()
		{
			((Node)this).Name = new StringName("VictoryNode");
		}

		public override void _Process(double delta)
		{
			if (++_frameCounter % 15 != 0)
			{
				return;
			}
			NRun instance = NRun.Instance;
			if (instance == null)
			{
				ResetState();
				return;
			}
			ulong instanceId = ((GodotObject)instance).GetInstanceId();
			if (instanceId != _lastRunNodeId)
			{
				_lastRunNodeId = instanceId;
				_missingGameOverTicks = 0;
				_summaryForcedForCurrentRun = false;
			}
			if (_summaryForcedForCurrentRun)
			{
				return;
			}
			RunState runState = GameStateAccessor.GetRunState();
			if (runState != null)
			{
				AbstractRoom currentRoom = runState.CurrentRoom;
				if (((currentRoom != null) ? new bool?(currentRoom.IsVictoryRoom) : ((bool?)null)) == true)
				{
					NOverlayStack instance2 = NOverlayStack.Instance;
					if (((instance2 != null) ? instance2.Peek() : null) is NGameOverScreen)
					{
						_missingGameOverTicks = 0;
						return;
					}
					if (runState.Players.Count <= 0 || !runState.Players.All((Player player) => player.Creature.IsDead))
					{
						_missingGameOverTicks = 0;
						return;
					}
					_missingGameOverTicks++;
					if (_missingGameOverTicks >= 12)
					{
						Log.Warn("[RMP:Victory] Victory summary did not appear in time. Forcing GameOverScreen.", 2);
						instance.ShowGameOverScreen(RunManager.Instance.ToSave((AbstractRoom)null));
						_summaryForcedForCurrentRun = true;
						_missingGameOverTicks = 0;
					}
					return;
				}
			}
			_missingGameOverTicks = 0;
		}

		private void ResetState()
		{
			_lastRunNodeId = 0uL;
			_missingGameOverTicks = 0;
			_summaryForcedForCurrentRun = false;
		}








	}

	private const int CheckIntervalFrames = 15;

	private const int GracePeriodTicks = 12;

	public string Name => "VictoryFlow";

	public void Initialize(ConfigManager config, ReflectionCache cache)
	{
	}

	public Node? CreateNode()
	{
		return new VictoryNode();
	}

	public void Cleanup()
	{
	}
}
