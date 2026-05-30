using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Game;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using Sts2TreasureRoom = MegaCrit.Sts2.Core.Rooms.TreasureRoom;
using MegaCrit.Sts2.Core.Runs;
using RemoveMultiplayerPlayerLimit.Core;
using RemoveMultiplayerPlayerLimit.Infrastructure;

namespace RemoveMultiplayerPlayerLimit.Features.TreasureRoom;

public partial class TreasureModule : IRMPModule
{
	private partial class TreasureNode : Node
	{



		private readonly TreasureModule _mod;

		private int _frameCounter;

		private NTreasureRoomRelicCollection? _lastCollection;

		private bool _layoutApplied;

		private RunLocationTargetedMessageBuffer? _registeredMessageBuffer;

		private MessageHandlerDelegate<TreasureChestOpenedMessage>? _treasureChestOpenedHandler;

		private readonly HashSet<string> _mirroredRemoteChestRewards = new HashSet<string>();

		public TreasureNode(TreasureModule mod)
		{
			_mod = mod;
			((Node)this).Name = new StringName("TreasureNode");
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
			UnregisterTreasureChestHandler();
		}

		private void OnNodeAdded(Node node)
		{
			NTreasureRoomRelicCollection val = (NTreasureRoomRelicCollection)(object)((node is NTreasureRoomRelicCollection) ? node : null);
			if (val == null)
			{
				return;
			}
			try
			{
				_mod.PrewarmAllPlayerStates();
			}
			catch (Exception ex)
			{
				Log.Warn("[RMP:Treasure] Pre-warm peer input states failed: " + ex.Message, 2);
			}
			try
			{
				ExpandHolders(val);
			}
			catch (Exception ex2)
			{
				Log.Warn("[RMP:Treasure] Pre-expand failed: " + ex2.Message, 2);
			}
		}

		public override void _Process(double delta)
		{
			EnsureTreasureChestHandler();
			if (++_frameCounter % 10 != 0)
			{
				return;
			}
			NTreasureRoomRelicCollection val = SceneMonitor.FindTreasureRoomRelicCollection();
			if (val == null || val != _lastCollection)
			{
				_lastCollection = val;
				_layoutApplied = false;
			}
			else
			{
				if (val == null)
				{
					return;
				}
				ExpandHolders(val);
				_mod.PrewarmAllPlayerStates();
				List<NTreasureRoomRelicHolder> holdersInUse = _mod.GetHoldersInUse(val);
				if (holdersInUse == null || holdersInUse.Count == 0)
				{
					return;
				}
				RunManager instance = RunManager.Instance;
				TreasureRoomRelicSynchronizer obj = ((instance != null) ? instance.TreasureRoomRelicSynchronizer : null);
				IReadOnlyList<RelicModel> readOnlyList = ((obj != null) ? obj.CurrentRelics : null);
				if (readOnlyList == null)
				{
					return;
				}
				try
				{
					if (!_layoutApplied && holdersInUse.Count < readOnlyList.Count)
					{
						BootstrapExtraHolders(val);
					}
					holdersInUse = _mod.GetHoldersInUse(val);
					if (holdersInUse != null && holdersInUse.Count >= readOnlyList.Count && !_layoutApplied)
					{
						ApplyLayout(val);
						_layoutApplied = true;
					}
				}
				catch (Exception ex)
				{
					Log.Warn("[RMP:Treasure] Bootstrap/layout failed (will retry): " + ex.Message, 2);
				}
			}
		}

		private void ExpandHolders(NTreasureRoomRelicCollection collection)
		{
			List<NTreasureRoomRelicHolder> multiplayerHolders = _mod.GetMultiplayerHolders(collection);
			if (multiplayerHolders == null || multiplayerHolders.Count == 0)
			{
				return;
			}
			RunManager instance = RunManager.Instance;
			object obj;
			if (instance == null)
			{
				obj = null;
			}
			else
			{
				TreasureRoomRelicSynchronizer treasureRoomRelicSynchronizer = instance.TreasureRoomRelicSynchronizer;
				obj = ((treasureRoomRelicSynchronizer != null) ? treasureRoomRelicSynchronizer.CurrentRelics : null);
			}
			IReadOnlyList<RelicModel> readOnlyList = (IReadOnlyList<RelicModel>)obj;
			if (readOnlyList == null || readOnlyList.Count <= multiplayerHolders.Count)
			{
				return;
			}
			NTreasureRoomRelicHolder val = multiplayerHolders[multiplayerHolders.Count - 1];
			string sceneFilePath = ((Node)val).SceneFilePath;
			PackedScene val2 = ((!string.IsNullOrEmpty(sceneFilePath)) ? PreloadManager.Cache.GetScene(sceneFilePath) : null);
			Node parent = ((Node)val).GetParent();
			for (int i = multiplayerHolders.Count; i < readOnlyList.Count; i++)
			{
				object obj3;
				if (val2 == null)
				{
					Node obj2 = ((Node)val).Duplicate(15);
					obj3 = ((obj2 is NTreasureRoomRelicHolder) ? obj2 : null);
				}
				else
				{
					obj3 = val2.Instantiate<NTreasureRoomRelicHolder>((PackedScene.GenEditState)0);
				}
				NTreasureRoomRelicHolder val3 = (NTreasureRoomRelicHolder)obj3;
				if (val3 != null)
				{
					((Node)val3).Name = new StringName($"AutoHolder_{i + 1}");
					((CanvasItem)val3).Visible = false;
					parent.AddChild((Node)(object)val3, false, Node.InternalMode.Disabled);
					multiplayerHolders.Add(val3);
				}
			}
		}

		private void BootstrapExtraHolders(NTreasureRoomRelicCollection collection)
		{
			List<NTreasureRoomRelicHolder> holdersInUse = _mod.GetHoldersInUse(collection);
			List<NTreasureRoomRelicHolder> multiplayerHolders = _mod.GetMultiplayerHolders(collection);
			IRunState runState = _mod.GetRunState(collection);
			RunManager instance = RunManager.Instance;
			object obj;
			if (instance == null)
			{
				obj = null;
			}
			else
			{
				TreasureRoomRelicSynchronizer treasureRoomRelicSynchronizer = instance.TreasureRoomRelicSynchronizer;
				obj = ((treasureRoomRelicSynchronizer != null) ? treasureRoomRelicSynchronizer.CurrentRelics : null);
			}
			IReadOnlyList<RelicModel> readOnlyList = (IReadOnlyList<RelicModel>)obj;
			if (holdersInUse == null || multiplayerHolders == null || runState == null || readOnlyList == null || readOnlyList.Count <= 4)
			{
				return;
			}
			for (int i = holdersInUse.Count; i < multiplayerHolders.Count && i < readOnlyList.Count; i++)
			{
				NTreasureRoomRelicHolder val = multiplayerHolders[i];
				try
				{
					if (val.Relic == null)
					{
						continue;
					}
					((CanvasItem)val).Visible = true;
					val.Relic.Model = readOnlyList[i];
					val.Initialize(readOnlyList[i], runState);
					val.Index = i;
					int idx = i;
					((GodotObject)val).Connect(NClickableControl.SignalName.Released, Callable.From<NButton>((Action<NButton>)delegate
					{
						RunManager instance2 = RunManager.Instance;
						TreasureRoomRelicSynchronizer val2 = ((instance2 != null) ? instance2.TreasureRoomRelicSynchronizer : null);
						if (((val2 != null) ? val2.CurrentRelics : null) != null)
						{
							val2.PickRelicLocally((int?)idx);
						}
					}), 0u);
					holdersInUse.Add(val);
					NMultiplayerVoteContainer voteContainer = val.VoteContainer;
					if (voteContainer != null)
					{
						voteContainer.RefreshPlayerVotes(true);
					}
				}
				catch (Exception ex)
				{
					Log.Warn($"[RMP:Treasure] Failed to bootstrap holder {i}: {ex.Message}", 2);
				}
			}
			RebuildHolderFocusNavigation(holdersInUse);
		}

		private void EnsureTreasureChestHandler()
		{
			RunManager instance = RunManager.Instance;
			RunLocationTargetedMessageBuffer val = ((instance != null) ? instance.RunLocationTargetedBuffer : null);
			if (val == _registeredMessageBuffer)
			{
				return;
			}
			UnregisterTreasureChestHandler();
			if (val == null)
			{
				return;
			}
			RunManager instance2 = RunManager.Instance;
			if (instance2 != null)
			{
				INetGameService netService = instance2.NetService;
				if (((netService != null) ? new bool?(NetGameTypeExtensions.IsMultiplayer(netService.Type)) : ((bool?)null)) == true)
				{
					_treasureChestOpenedHandler = HandleTreasureChestOpened;
					_registeredMessageBuffer = val;
					val.RegisterMessageHandler<TreasureChestOpenedMessage>(_treasureChestOpenedHandler);
				}
			}
		}

		private void UnregisterTreasureChestHandler()
		{
			if (_registeredMessageBuffer != null && _treasureChestOpenedHandler != null)
			{
				try
				{
					_registeredMessageBuffer.UnregisterMessageHandler<TreasureChestOpenedMessage>(_treasureChestOpenedHandler);
				}
				catch
				{
				}
			}
			_registeredMessageBuffer = null;
			_treasureChestOpenedHandler = null;
			_mirroredRemoteChestRewards.Clear();
		}

		private void HandleTreasureChestOpened(TreasureChestOpenedMessage message, ulong senderId)
		{
			RunState runState = GameStateAccessor.GetRunState();
			if (runState == null || runState.Players.Count <= 1)
			{
				return;
			}
			Player me = LocalContext.GetMe((IPlayerCollection)(object)runState);
			if (me == null || me.NetId == senderId)
			{
				return;
			}
			AbstractRoom currentRoom = runState.CurrentRoom;
			Sts2TreasureRoom val = currentRoom as Sts2TreasureRoom;
			Player val2 = runState.Players.FirstOrDefault((Player candidate) => candidate.NetId == senderId);
			if (val != null && val2 != null)
			{
				string item = $"{message.Location}:{senderId}";
				if (_mirroredRemoteChestRewards.Add(item))
				{
					TaskHelper.RunSafely(MirrorRemoteTreasureRoomEndRewards(val, val2));
				}
			}
		}

		private async Task MirrorRemoteTreasureRoomEndRewards(Sts2TreasureRoom room, Player player)
		{
			await RewardsCmd.OfferForRoomEnd(player, (AbstractRoom)(object)room);
		}

		private void ApplyLayout(NTreasureRoomRelicCollection collection)
		{
			List<NTreasureRoomRelicHolder> holdersInUse = _mod.GetHoldersInUse(collection);
			if (holdersInUse == null || holdersInUse.Count <= 4)
			{
				return;
			}
			float num = float.MaxValue;
			float num2 = float.MinValue;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			for (int i = 0; i < 4 && i < holdersInUse.Count; i++)
			{
				Vector2 position = ((Control)holdersInUse[i]).Position;
				num = Math.Min(num, position.X);
				num2 = Math.Max(num2, position.X);
				num3 = Math.Min(num3, position.Y);
				num4 = Math.Max(num4, position.Y);
			}
			int count = holdersInUse.Count;
			int num5 = Math.Max(1, Math.Min(4, count));
			int num6 = (int)Math.Ceiling((float)count / (float)num5);
			float num7 = (num + num2) * 0.5f;
			float num8 = (num3 + num4) * 0.5f;
			float num9 = (num2 - num) / (float)Math.Max(1, num5 - 1);
			num9 = ((num9 > 0f) ? Math.Max(190f, num9) : 220f);
			float num10 = 0f;
			if (num6 > 1)
			{
				float num11 = 0f;
				Vector2 combinedMinimumSize = ((Control)holdersInUse[0]).GetCombinedMinimumSize();
				if (combinedMinimumSize.Y > 0f)
				{
					num11 = combinedMinimumSize.Y;
				}
				float val = ((num11 > 0f) ? (num11 * 1.05f) : 0f);
				num10 = Math.Max(120f, Math.Max(Math.Abs(num4 - num3), val));
				if (num10 * (float)(num6 - 1) > 640f)
				{
					num10 = 640f / (float)(num6 - 1);
				}
			}
			int num12 = 0;
			for (int j = 0; j < num6; j++)
			{
				int num13 = Math.Min(num5, count - num12);
				float num14 = num8 + ((float)j - (float)(num6 - 1) * 0.5f) * num10;
				float num15 = num7 - (float)(num13 - 1) * num9 * 0.5f;
				for (int k = 0; k < num13; k++)
				{
					((Control)holdersInUse[num12 + k]).Position = new Vector2(num15 + (float)k * num9, num14);
				}
				num12 += num13;
			}
			RebuildHolderFocusNavigation(holdersInUse);
		}

		private static void RebuildHolderFocusNavigation(List<NTreasureRoomRelicHolder> holdersInUse)
		{
			if (holdersInUse.Count == 0)
			{
				return;
			}
			for (int i = 0; i < holdersInUse.Count; i++)
			{
				NTreasureRoomRelicHolder obj = holdersInUse[i];
				((Control)obj).SetFocusMode((Control.FocusModeEnum)2);
				((Control)obj).FocusNeighborTop = ((Node)obj).GetPath();
				((Control)obj).FocusNeighborBottom = ((Node)obj).GetPath();
				NodePath path;
				if (i <= 0)
				{
					path = ((Node)holdersInUse[holdersInUse.Count - 1]).GetPath();
				}
				else
				{
					path = ((Node)holdersInUse[i - 1]).GetPath();
				}
				((Control)obj).FocusNeighborLeft = path;
				((Control)obj).FocusNeighborRight = ((i < holdersInUse.Count - 1) ? ((Node)holdersInUse[i + 1]).GetPath() : ((Node)holdersInUse[0]).GetPath());
			}
		}








	}

	private const float FallbackXStep = 220f;

	private const float MinXStep = 190f;

	private const float MinYStep = 120f;

	private ReflectionCache _cache;

	internal FieldInfo? HoldersInUseField;

	internal FieldInfo? MultiplayerHoldersField;

	internal FieldInfo? RunStateField;

	internal FieldInfo? SyncPlayerCollectionField;

	internal FieldInfo? SyncLocalPlayerIdField;

	internal FieldInfo? SyncActionQueueField;

	internal FieldInfo? SyncCurrentRelicsField;

	internal FieldInfo? SyncRngField;

	internal FieldInfo? SyncVotesField;

	internal FieldInfo? SyncPredictedVoteField;

	internal FieldInfo? VotesChangedEventField;

	internal FieldInfo? RelicsAwardedEventField;

	internal MethodInfo? EndRelicVotingMethod;

	internal MethodInfo? PeerInputGetOrCreateMethod;

	internal readonly HashSet<TreasureRoomRelicSynchronizer> LocalVotePending = new HashSet<TreasureRoomRelicSynchronizer>();

	internal readonly HashSet<TreasureRoomRelicSynchronizer> LocalSkipLocked = new HashSet<TreasureRoomRelicSynchronizer>();

	public string Name => "TreasureRoom";

	public void Initialize(ConfigManager config, ReflectionCache cache)
	{
		_cache = cache;
		Type typeFromHandle = typeof(NTreasureRoomRelicCollection);
		Type typeFromHandle2 = typeof(TreasureRoomRelicSynchronizer);
		HoldersInUseField = cache.GetField(typeFromHandle, "_holdersInUse");
		MultiplayerHoldersField = cache.GetField(typeFromHandle, "_multiplayerHolders");
		RunStateField = cache.GetField(typeFromHandle, "_runState");
		SyncPlayerCollectionField = cache.GetField(typeFromHandle2, "_playerCollection");
		SyncLocalPlayerIdField = cache.GetField(typeFromHandle2, "_localPlayerId");
		SyncActionQueueField = cache.GetField(typeFromHandle2, "_actionQueueSynchronizer");
		SyncCurrentRelicsField = cache.GetField(typeFromHandle2, "_currentRelics");
		SyncRngField = cache.GetField(typeFromHandle2, "_rng");
		SyncVotesField = cache.GetField(typeFromHandle2, "_votes");
		SyncPredictedVoteField = cache.GetField(typeFromHandle2, "_predictedVote");
		VotesChangedEventField = cache.GetField(typeFromHandle2, "VotesChanged");
		RelicsAwardedEventField = cache.GetField(typeFromHandle2, "RelicsAwarded");
		EndRelicVotingMethod = cache.GetMethod(typeFromHandle2, "EndRelicVoting");
		PeerInputGetOrCreateMethod = typeof(PeerInputSynchronizer).GetMethod("GetOrCreateStateForPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
	}

	internal void PrewarmAllPlayerStates()
	{
		if (PeerInputGetOrCreateMethod == null)
		{
			return;
		}
		RunManager instance = RunManager.Instance;
		PeerInputSynchronizer val = ((instance != null) ? instance.InputSynchronizer : null);
		if (val == null)
		{
			return;
		}
		RunState runState = GameStateAccessor.GetRunState();
		if (((runState != null) ? runState.Players : null) == null)
		{
			return;
		}
		foreach (Player player in runState.Players)
		{
			try
			{
				PeerInputGetOrCreateMethod.Invoke(val, new object[1] { player.NetId });
			}
			catch
			{
			}
		}
	}

	public Node? CreateNode()
	{
		return (Node?)(object)new TreasureNode(this);
	}

	public void Cleanup()
	{
		LocalVotePending.Clear();
		LocalSkipLocked.Clear();
	}

	internal List<NTreasureRoomRelicHolder>? GetHoldersInUse(NTreasureRoomRelicCollection c)
	{
		return HoldersInUseField?.GetValue(c) as List<NTreasureRoomRelicHolder>;
	}

	internal List<NTreasureRoomRelicHolder>? GetMultiplayerHolders(NTreasureRoomRelicCollection c)
	{
		return MultiplayerHoldersField?.GetValue(c) as List<NTreasureRoomRelicHolder>;
	}

	internal IRunState? GetRunState(NTreasureRoomRelicCollection c)
	{
		object? obj = RunStateField?.GetValue(c);
		return (IRunState?)((obj is IRunState) ? obj : null);
	}

	internal IPlayerCollection? GetSyncPlayerCollection(TreasureRoomRelicSynchronizer s)
	{
		object? obj = SyncPlayerCollectionField?.GetValue(s);
		return (IPlayerCollection?)((obj is IPlayerCollection) ? obj : null);
	}

	internal ulong? GetSyncLocalPlayerId(TreasureRoomRelicSynchronizer s)
	{
		object obj = SyncLocalPlayerIdField?.GetValue(s);
		if (!(obj is ulong))
		{
			return null;
		}
		return (ulong)obj;
	}

	internal ActionQueueSynchronizer? GetSyncActionQueue(TreasureRoomRelicSynchronizer s)
	{
		object? obj = SyncActionQueueField?.GetValue(s);
		return (ActionQueueSynchronizer?)((obj is ActionQueueSynchronizer) ? obj : null);
	}

	internal List<RelicModel>? GetSyncCurrentRelics(TreasureRoomRelicSynchronizer s)
	{
		return SyncCurrentRelicsField?.GetValue(s) as List<RelicModel>;
	}

	internal Rng? GetSyncRng(TreasureRoomRelicSynchronizer s)
	{
		object? obj = SyncRngField?.GetValue(s);
		return (Rng?)((obj is Rng) ? obj : null);
	}

	internal List<int?>? GetSyncVotes(TreasureRoomRelicSynchronizer s)
	{
		return SyncVotesField?.GetValue(s) as List<int?>;
	}

	internal void SetSyncPredictedVote(TreasureRoomRelicSynchronizer s, int? vote)
	{
		if (!(SyncPredictedVoteField == null))
		{
			Type fieldType = SyncPredictedVoteField.FieldType;
			if (fieldType == typeof(int?))
			{
				SyncPredictedVoteField.SetValue(s, vote);
			}
			else if (fieldType == typeof(int))
			{
				SyncPredictedVoteField.SetValue(s, vote ?? (-1));
			}
		}
	}

	internal void InvokeVotesChanged(TreasureRoomRelicSynchronizer s)
	{
		if (VotesChangedEventField?.GetValue(s) is Action action)
		{
			action();
		}
	}

	internal void InvokeRelicsAwarded(TreasureRoomRelicSynchronizer s, List<RelicPickingResult> results)
	{
		if (RelicsAwardedEventField?.GetValue(s) is Action<List<RelicPickingResult>> action)
		{
			action(results);
		}
	}

	internal void InvokeEndRelicVoting(TreasureRoomRelicSynchronizer s)
	{
		EndRelicVotingMethod?.Invoke(s, null);
	}

	internal void ClearLocalVoteState(TreasureRoomRelicSynchronizer s)
	{
		LocalVotePending.Remove(s);
		LocalSkipLocked.Remove(s);
		SetSyncPredictedVote(s, null);
	}
}
