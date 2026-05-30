using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using RemoveMultiplayerPlayerLimit.Core;
using RemoveMultiplayerPlayerLimit.Infrastructure;

namespace RemoveMultiplayerPlayerLimit.Features.ShopLayout;

public partial class ShopModule : IRMPModule
{
	private partial class ShopNode : Node
	{



		private readonly ShopModule _module;

		private int _frameCounter;

		private bool _arranged;

		private NMerchantRoom? _lastRoom;

		public ShopNode(ShopModule module)
		{
			_module = module;
			((Node)this).Name = new StringName("ShopNode");
		}

		public override void _Process(double delta)
		{
			if (++_frameCounter % 10 != 0)
			{
				return;
			}
			NMerchantRoom val = SceneMonitor.FindMerchantRoom();
			if (val == null || val != _lastRoom)
			{
				_arranged = false;
				_lastRoom = val;
			}
			else if (!_arranged)
			{
				try
				{
					RepositionVisuals(val);
					_arranged = true;
				}
				catch (Exception value)
				{
					Log.Warn($"[RMP:Shop] Failed to reposition visuals: {value}", 2);
					_arranged = true;
				}
			}
		}

		private void RepositionVisuals(NMerchantRoom room)
		{
			IReadOnlyList<NMerchantCharacter> playerVisuals = room.PlayerVisuals;
			if (playerVisuals.Count <= 4)
			{
				return;
			}
			int num = ((playerVisuals.Count <= 8) ? 2 : Mathf.CeilToInt((float)playerVisuals.Count / 4f));
			int num2 = Mathf.CeilToInt((float)playerVisuals.Count / (float)num);
			int num3 = 0;
			for (int i = 0; i < num; i++)
			{
				float num4 = 160f + -110f * (float)i;
				float num5 = 35f + -40f * (float)i;
				for (int j = 0; j < num2; j++)
				{
					if (num3 >= playerVisuals.Count)
					{
						break;
					}
					((Node2D)playerVisuals[num3]).Position = new Vector2(num4, num5);
					num4 += -230f;
					num3++;
				}
			}
		}








	}

	private const float ForwardShiftX = 160f;

	private const float ForwardShiftY = 35f;

	private const float RowStartOffsetX = -110f;

	private const float RowStepY = -40f;

	private const float ColumnStepX = -230f;

	private ReflectionCache _cache;

	public string Name => "ShopLayout";

	public void Initialize(ConfigManager config, ReflectionCache cache)
	{
		_cache = cache;
	}

	public Node? CreateNode()
	{
		return (Node?)(object)new ShopNode(this);
	}

	public void Cleanup()
	{
	}
}
