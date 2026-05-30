using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using RemoveMultiplayerPlayerLimit.Core;
using RemoveMultiplayerPlayerLimit.Infrastructure;

namespace RemoveMultiplayerPlayerLimit.Features.CampfireLayout;

public partial class CampfireModule : IRMPModule
{
	private partial class CampfireNode : Node
	{



		private readonly CampfireModule _module;

		private int _frameCounter;

		private bool _arranged;

		private NRestSiteRoom? _lastRoom;

		public CampfireNode(CampfireModule module)
		{
			_module = module;
			((Node)this).Name = new StringName("CampfireNode");
		}

		public override void _EnterTree()
		{
			((Node)this).GetTree().NodeAdded += new SceneTree.NodeAddedEventHandler(OnNodeAdded);
		}

		public override void _ExitTree()
		{
			SceneTree tree = ((Node)this).GetTree();
			if (tree != null)
			{
				tree.NodeAdded -= new SceneTree.NodeAddedEventHandler(OnNodeAdded);
			}
		}

		private void OnNodeAdded(Node node)
		{
			NRestSiteRoom val = (NRestSiteRoom)(object)((node is NRestSiteRoom) ? node : null);
			if (val == null)
			{
				return;
			}
			int playerCount = GameStateAccessor.GetPlayerCount();
			if (playerCount <= 4)
			{
				return;
			}
			if (_module._containersField == null)
			{
				Log.Warn("[RMP:Campfire] _characterContainers field not found — cannot prevent crash.", 2);
				return;
			}
			try
			{
				PreInjectContainers(val, playerCount);
			}
			catch (Exception ex)
			{
				Log.Warn("[RMP:Campfire] Pre-injection failed: " + ex.Message, 2);
			}
		}

		private void PreInjectContainers(NRestSiteRoom restSite, int playerCount)
		{
			Control nodeOrNull = ((Node)restSite).GetNodeOrNull<Control>(new NodePath("BgContainer"));
			if (nodeOrNull == null)
			{
				return;
			}
			List<Control> list = new List<Control>();
			for (int i = 1; i <= 4; i++)
			{
				Control nodeOrNull2 = ((Node)nodeOrNull).GetNodeOrNull<Control>(new NodePath($"Character_{i}"));
				if (nodeOrNull2 != null)
				{
					list.Add(nodeOrNull2);
				}
			}
			if (list.Count >= 4)
			{
				for (int j = list.Count; j < playerCount; j++)
				{
					Control val = new Control();
					((Node)val).Name = new StringName($"Character_{j + 1}");
					val.Position = GetExtraContainerPosition(list, j);
					((Node)nodeOrNull).AddChild((Node)(object)val, false, Node.InternalMode.Disabled);
					list.Add(val);
				}
				if (_module._containersField.GetValue(restSite) is List<Control> list2)
				{
					list2.AddRange(list);
				}
				Log.Info($"[RMP:Campfire] Pre-injected {playerCount - 4} extra containers for {playerCount} players", 2);
			}
		}

		public override void _Process(double delta)
		{
			if (++_frameCounter % 10 != 0)
			{
				return;
			}
			NRestSiteRoom val = SceneMonitor.FindRestSiteRoom();
			if (val == null || val != _lastRoom)
			{
				_arranged = false;
				_lastRoom = val;
			}
			else
			{
				if (_arranged)
				{
					return;
				}
				int playerCount = GameStateAccessor.GetPlayerCount();
				if (playerCount > 4)
				{
					try
					{
						ArrangeVisuals(val, playerCount);
						_arranged = true;
					}
					catch (Exception value)
					{
						Log.Warn($"[RMP:Campfire] Failed to arrange visuals: {value}", 2);
						_arranged = true;
					}
				}
			}
		}

		private void ArrangeVisuals(NRestSiteRoom room, int playerCount)
		{
			if (!(_module._containersField == null) && _module._containersField.GetValue(room) is List<Control> { Count: not 0 } list)
			{
				if (list.Count < playerCount)
				{
					EnsureContainers(list, playerCount);
				}
				Control parent = ((Node)list[0]).GetParent<Control>();
				if (parent != null)
				{
					EnsureExtraLogs(parent);
				}
			}
		}

		private static void EnsureContainers(List<Control> containers, int requiredCount)
		{
			if (requiredCount <= containers.Count)
			{
				return;
			}
			Control parent = ((Node)containers[0]).GetParent<Control>();
			if (parent == null)
			{
				return;
			}
			int num = Math.Min(containers.Count, 4);
			if (num != 0)
			{
				while (containers.Count < requiredCount)
				{
					int count = containers.Count;
					Node obj = ((Node)containers[count % num]).Duplicate(15);
					Control val = (Control)(((object)((obj is Control) ? obj : null)) ?? ((object)new Control()));
					RemoveAllChildren((Node)(object)val);
					((Node)val).Name = new StringName($"Character_Auto_{count + 1}");
					val.Position = GetExtraContainerPosition(containers, count);
					((Node)parent).AddChild((Node)(object)val, false, Node.InternalMode.Disabled);
					containers.Add(val);
				}
			}
		}

		private static Vector2 GetExtraContainerPosition(List<Control> containers, int index)
		{
			if (containers.Count < 4)
			{
				return containers[containers.Count - 1].Position;
			}
			if (index < 4)
			{
				return containers[index].Position;
			}
			int num = index - 4;
			bool flag = num % 2 == 0;
			int num2 = num / 2;
			Vector2 result = (flag ? (containers[0].Position + LeftExtraFrontOffset) : (containers[1].Position + RightExtraFrontOffset));
			Vector2 val = (flag ? (containers[2].Position + LeftExtraBackOffset) : (containers[3].Position + RightExtraBackOffset));
			switch (num2)
			{
			case 0:
				return result;
			case 1:
				return val;
			default:
			{
				int num3 = num2 - 1;
				Vector2 offset = new Vector2((flag ? -1f : 1f) * ExtraSeatStep.X * num3, ExtraSeatStep.Y * num3);
				return val + offset;
			}
			}
		}

		private static void EnsureExtraLogs(Control parent)
		{
			Node val = ((((Node)parent).GetChildCount(false) > 0) ? ((Node)parent).GetChild(0, false) : null);
			if (val != null && val.GetNodeOrNull<Node>(new NodePath("AutoExtraLogsMarker")) == null)
			{
				Node val2 = new Node
				{
					Name = new StringName("AutoExtraLogsMarker")
				};
				val.AddChild(val2, false, Node.InternalMode.Disabled);
				DuplicateShiftedNode(val, "RestSiteLLog", LogXOffsetLeft, "AutoL");
				DuplicateShiftedNode(val, "RestSiteRLog", LogXOffsetRight, "AutoR");
				DuplicateShiftedNode(val, "RestSiteLighting/RestSiteLLog2", LogXOffsetLeft, "AutoL");
				DuplicateShiftedNode(val, "RestSiteLighting/RestSiteRLog2", LogXOffsetRight, "AutoR");
			}
		}

		private static void DuplicateShiftedNode(Node root, string nodePath, Vector2 offset, string suffix)
		{
			Node nodeOrNull = root.GetNodeOrNull<Node>(new NodePath(nodePath));
			if (nodeOrNull == null)
			{
				return;
			}
			Node parent = nodeOrNull.GetParent();
			if (parent == null)
			{
				return;
			}
			Node val = nodeOrNull.Duplicate(15);
			val.Name = new StringName($"{nodeOrNull.Name}_{suffix}");
			parent.AddChild(val, false, Node.InternalMode.Disabled);
			Control val2 = (Control)(object)((nodeOrNull is Control) ? nodeOrNull : null);
			if (val2 != null)
			{
				Control val3 = (Control)(object)((val is Control) ? val : null);
				if (val3 != null)
				{
					val3.Position = val2.Position + offset;
					return;
				}
			}
			Node2D val4 = (Node2D)(object)((nodeOrNull is Node2D) ? nodeOrNull : null);
			if (val4 != null)
			{
				Node2D val5 = (Node2D)(object)((val is Node2D) ? val : null);
				if (val5 != null)
				{
					val5.Position = val4.Position + offset;
				}
			}
		}

		private static void RemoveAllChildren(Node node)
		{
			for (int num = node.GetChildCount(false) - 1; num >= 0; num--)
			{
				Node child = node.GetChild(num, false);
				node.RemoveChild(child);
				child.QueueFree();
			}
		}









	}

	private static readonly Vector2 LeftExtraFrontOffset = new Vector2(-250f, 35f);

	private static readonly Vector2 LeftExtraBackOffset = new Vector2(-240f, -20f);

	private static readonly Vector2 RightExtraFrontOffset = new Vector2(250f, 35f);

	private static readonly Vector2 RightExtraBackOffset = new Vector2(240f, -20f);

	private static readonly Vector2 LogXOffsetLeft = new Vector2(-250f, 0f);

	private static readonly Vector2 LogXOffsetRight = new Vector2(250f, 0f);

	private static readonly Vector2 ExtraSeatStep = new Vector2(70f, -45f);

	private FieldInfo? _containersField;

	private ReflectionCache _cache;

	public string Name => "CampfireLayout";

	public void Initialize(ConfigManager config, ReflectionCache cache)
	{
		_cache = cache;
		_containersField = cache.GetField(typeof(NRestSiteRoom), "_characterContainers");
	}

	public Node? CreateNode()
	{
		return (Node?)(object)new CampfireNode(this);
	}

	public void Cleanup()
	{
	}
}
