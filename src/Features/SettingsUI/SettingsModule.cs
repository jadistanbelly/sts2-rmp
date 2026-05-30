using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.addons.mega_text;
using RemoveMultiplayerPlayerLimit.Core;
using RemoveMultiplayerPlayerLimit.Infrastructure;

namespace RemoveMultiplayerPlayerLimit.Features.SettingsUI;

public partial class SettingsModule : IRMPModule
{
	private partial class SettingsNode : Node
	{



		private readonly SettingsModule _mod;

		private int _frameCounter;

		private bool _injected;

		private NSettingsScreen? _lastScreen;

		public SettingsNode(SettingsModule mod)
		{
			_mod = mod;
			((Node)this).Name = new StringName("SettingsNode");
		}

		public override void _Process(double delta)
		{
			if (++_frameCounter % 30 != 0)
			{
				return;
			}
			NSettingsScreen val = SceneMonitor.FindSettingsScreen();
			if (val == null && _lastScreen != null)
			{
				_mod._config.Save();
				_injected = false;
				_lastScreen = null;
				return;
			}
			if (val != _lastScreen)
			{
				_lastScreen = val;
				_injected = false;
			}
			if (val == null || _injected)
			{
				return;
			}
			try
			{
				InjectSettings(val);
				_injected = true;
			}
			catch (Exception value)
			{
				Log.Warn($"[RMP:Settings] Injection failed: {value}", 2);
				_injected = true;
			}
		}

		private void InjectSettings(NSettingsScreen screen)
		{
			NSettingsPanel node = ((Node)screen).GetNode<NSettingsPanel>(new NodePath("%GeneralSettings"));
			VBoxContainer content = node.Content;
			RemoveExistingInjectedControls(content);
			Control val = ((Node)screen).GetNodeOrNull<Control>(new NodePath("%Modding")) ?? ((Node)screen).GetNodeOrNull<Control>(new NodePath("%SendFeedback"));
			if (val == null)
			{
				Log.Warn("[RMP:Settings] Anchor node not found.", 2);
				return;
			}
			int num = ((Node)val).GetIndex(false) + 1;
			RichTextLabel val2 = ((Node)content).GetNodeOrNull<RichTextLabel>(new NodePath("Screenshake/Label")) ?? ((Node)val).GetNodeOrNull<RichTextLabel>(new NodePath("Label"));
			ColorRect val3 = new ColorRect
			{
				Name = new StringName("RmpDivider"),
				CustomMinimumSize = new Vector2(0f, 2f),
				MouseFilter = (Control.MouseFilterEnum)2,
				Color = DividerColor
			};
			((Node)content).AddChild((Node)(object)val3, false, Node.InternalMode.Disabled);
			((Node)content).MoveChild((Node)(object)val3, num);
			MarginContainer val4 = CreateSettingsRow("RmpDifficultyScaling");
			if (val2 != null)
			{
				RichTextLabel val5 = (RichTextLabel)((Node)val2).Duplicate(15);
				val5.Text = Localization.Get("SETTINGS_DIFFICULTY_SCALING_LABEL", "Difficulty Scaling");
				((Control)val5).MouseFilter = (Control.MouseFilterEnum)2;
				((Node)val4).AddChild((Node)(object)val5, false, Node.InternalMode.Disabled);
			}
			NPaginator val6 = CreateModPaginator("DifficultyScalingPaginator");
			if (val6 != null)
			{
				((Node)val4).AddChild((Node)(object)val6, false, Node.InternalMode.Disabled);
				((Node)content).AddChild((Node)(object)val4, false, Node.InternalMode.Disabled);
				((Node)content).MoveChild((Node)(object)val4, num + 1);
				SetupDifficultyPaginator(val6);
			}
			RebuildFocusChain(node);
		}

		private static void RemoveExistingInjectedControls(VBoxContainer vbox)
		{
			RemoveInjectedControl(vbox, "RmpDivider");
			RemoveInjectedControl(vbox, "RmpDifficultyScaling");
			RemoveInjectedControl(vbox, "RmpPlayerLimit");
		}

		private static void RemoveInjectedControl(VBoxContainer vbox, string controlName)
		{
			Control nodeOrNull = ((Node)vbox).GetNodeOrNull<Control>(new NodePath(controlName));
			if (nodeOrNull != null)
			{
				((Node)vbox).RemoveChild((Node)(object)nodeOrNull);
				((Node)nodeOrNull).QueueFree();
			}
		}

		private static MarginContainer CreateSettingsRow(string name)
		{
			MarginContainer val = new MarginContainer
			{
				Name = new StringName(name),
				CustomMinimumSize = new Vector2(0f, 64f)
			};
			((Control)val).AddThemeConstantOverride(new StringName("margin_left"), 12);
			((Control)val).AddThemeConstantOverride(new StringName("margin_top"), 0);
			((Control)val).AddThemeConstantOverride(new StringName("margin_right"), 12);
			((Control)val).AddThemeConstantOverride(new StringName("margin_bottom"), 0);
			return val;
		}

		private NPaginator? CreateModPaginator(string name)
		{
			PackedScene val = ResourceLoader.Load<PackedScene>(SceneHelper.GetScenePath("screens/paginator"), (string)null, (ResourceLoader.CacheMode)1);
			if (val == null)
			{
				return null;
			}
			Node val2 = val.Instantiate((PackedScene.GenEditState)0);
			RmpPaginator rmpPaginator = new RmpPaginator(delegate(NPaginator p, int idx)
			{
				OnPaginatorChanged(p, idx);
			});
			((Node)rmpPaginator).Name = new StringName(name);
			((Control)rmpPaginator).CustomMinimumSize = new Vector2(324f, 64f);
			((Control)rmpPaginator).SizeFlagsHorizontal = (Control.SizeFlags)8;
			((Control)rmpPaginator).FocusMode = (Control.FocusModeEnum)2;
			((Control)rmpPaginator).MouseFilter = (Control.MouseFilterEnum)2;
			RmpPaginator rmpPaginator2 = rmpPaginator;
			foreach (Node item in new List<Node>((IEnumerable<Node>)val2.GetChildren(false)))
			{
				val2.RemoveChild(item);
				((Node)rmpPaginator2).AddChild(item, false, Node.InternalMode.Disabled);
				AdoptOwnership(item, val2, (Node)(object)rmpPaginator2);
			}
			((GodotObject)val2).Free();
			return (NPaginator?)(object)rmpPaginator2;
		}

		private static void AdoptOwnership(Node node, Node oldOwner, Node newOwner)
		{
			if (node.Owner == oldOwner)
			{
				node.Owner = newOwner;
			}
			foreach (Node child in node.GetChildren(false))
			{
				AdoptOwnership(child, oldOwner, newOwner);
			}
		}

		private void SetupDifficultyPaginator(NPaginator paginator)
		{
			if (_mod._paginatorOptionsField?.GetValue(paginator) is List<string> list)
			{
				list.Clear();
				list.Add("OFF");
				list.Add("ON");
				int num = (ProtocolConfig.DifficultyScalingEnabled ? 1 : 0);
				_mod._paginatorCurrentIndexField?.SetValue(paginator, num);
				object? obj = _mod._paginatorLabelField?.GetValue(paginator);
				MegaLabel val = (MegaLabel)((obj is MegaLabel) ? obj : null);
				if (val != null)
				{
					val.SetTextAutoSize(list[num]);
				}
				_mod._difficultyPaginators.Add(paginator);
				((Node)paginator).TreeExiting += delegate
				{
					_mod._difficultyPaginators.Remove(paginator);
				};
			}
		}

		private void OnPaginatorChanged(NPaginator paginator, int index)
		{
			if (_mod._difficultyPaginators.Contains(paginator) && _mod._paginatorOptionsField?.GetValue(paginator) is List<string> list && index >= 0 && index < list.Count)
			{
				object? obj = _mod._paginatorLabelField?.GetValue(paginator);
				MegaLabel val = (MegaLabel)((obj is MegaLabel) ? obj : null);
				if (val != null)
				{
					val.SetTextAutoSize(list[index]);
				}
				ProtocolConfig.SetDifficultyScalingEnabled(list[index] == "ON");
				_mod._config.Save();
			}
		}

		private void RebuildFocusChain(NSettingsPanel panel)
		{
			if (!(_mod._getSettingsOptionsMethod == null) && !(_mod._panelFirstControlField == null))
			{
				List<Control> list = new List<Control>();
				_mod._getSettingsOptionsMethod.Invoke(panel, new object[2] { panel.Content, list });
				for (int i = 0; i < list.Count; i++)
				{
					list[i].FocusNeighborLeft = ((Node)list[i]).GetPath();
					list[i].FocusNeighborRight = ((Node)list[i]).GetPath();
					list[i].FocusNeighborTop = ((i > 0) ? ((Node)list[i - 1]).GetPath() : ((Node)list[i]).GetPath());
					list[i].FocusNeighborBottom = ((i < list.Count - 1) ? ((Node)list[i + 1]).GetPath() : ((Node)list[i]).GetPath());
				}
				if (list.Count > 0)
				{
					_mod._panelFirstControlField.SetValue(panel, list[0]);
				}
			}
		}









	}

	private partial class RmpPaginator : NPaginator
	{



		private readonly Action<NPaginator, int> _callback;

		public RmpPaginator(Action<NPaginator, int> callback)
		{
			_callback = callback;
		}

		public override void _Ready()
		{
			ConnectSignals();
		}

		protected override void OnIndexChanged(int index)
		{
			_callback((NPaginator)(object)this, index);
		}





	}

	private static readonly Color DividerColor = new Color(0.91f, 0.86f, 0.75f, 0.25f);

	private const string DividerName = "RmpDivider";

	private const string DifficultyScalingRowName = "RmpDifficultyScaling";

	private ReflectionCache _cache;

	private ConfigManager _config;

	private FieldInfo? _paginatorOptionsField;

	private FieldInfo? _paginatorCurrentIndexField;

	private FieldInfo? _paginatorLabelField;

	private MethodInfo? _getSettingsOptionsMethod;

	private FieldInfo? _panelFirstControlField;

	private readonly HashSet<NPaginator> _difficultyPaginators = new HashSet<NPaginator>();

	public string Name => "SettingsUI";

	public void Initialize(ConfigManager config, ReflectionCache cache)
	{
		_cache = cache;
		_config = config;
		_paginatorOptionsField = cache.GetField(typeof(NPaginator), "_options");
		_paginatorCurrentIndexField = cache.GetField(typeof(NPaginator), "_currentIndex");
		_paginatorLabelField = cache.GetField(typeof(NPaginator), "_label");
		_getSettingsOptionsMethod = cache.GetMethod(typeof(NSettingsPanel), "GetSettingsOptionsRecursive");
		_panelFirstControlField = cache.GetField(typeof(NSettingsPanel), "_firstControl");
	}

	public Node? CreateNode()
	{
		return (Node?)(object)new SettingsNode(this);
	}

	public void Cleanup()
	{
		_difficultyPaginators.Clear();
	}
}
