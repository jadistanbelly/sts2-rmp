using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.DailyRun;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;

namespace RemoveMultiplayerPlayerLimit.Infrastructure;

[ScriptPath("res://src/Infrastructure/SceneRegistry.cs")]
public partial class SceneRegistry : Node
{



	private NSettingsScreen? _settingsScreen;

	private NRestSiteRoom? _restSiteRoom;

	private NMerchantRoom? _merchantRoom;

	private NTreasureRoomRelicCollection? _treasureRoomRelicCollection;

	private NCharacterSelectScreen? _characterSelectScreen;

	private NCustomRunScreen? _customRunScreen;

	private NDailyRunScreen? _dailyRunScreen;

	private NMultiplayerLoadGameScreen? _multiplayerLoadGameScreen;

	private NCustomRunLoadScreen? _customRunLoadScreen;

	private NDailyRunLoadScreen? _dailyRunLoadScreen;

	private NMultiplayerSubmenu? _multiplayerSubmenu;

	private NMultiplayerHostSubmenu? _multiplayerHostSubmenu;

	public static SceneRegistry? Instance { get; private set; }

	internal NSettingsScreen? SettingsScreen => Validate<NSettingsScreen>(ref _settingsScreen);

	internal NRestSiteRoom? RestSiteRoom => Validate<NRestSiteRoom>(ref _restSiteRoom);

	internal NMerchantRoom? MerchantRoom => Validate<NMerchantRoom>(ref _merchantRoom);

	internal NTreasureRoomRelicCollection? TreasureRoomRelicCollection => Validate<NTreasureRoomRelicCollection>(ref _treasureRoomRelicCollection);

	internal NCharacterSelectScreen? CharacterSelectScreen => Validate<NCharacterSelectScreen>(ref _characterSelectScreen);

	internal NCustomRunScreen? CustomRunScreen => Validate<NCustomRunScreen>(ref _customRunScreen);

	internal NDailyRunScreen? DailyRunScreen => Validate<NDailyRunScreen>(ref _dailyRunScreen);

	internal NMultiplayerLoadGameScreen? MultiplayerLoadGameScreen => Validate<NMultiplayerLoadGameScreen>(ref _multiplayerLoadGameScreen);

	internal NCustomRunLoadScreen? CustomRunLoadScreen => Validate<NCustomRunLoadScreen>(ref _customRunLoadScreen);

	internal NDailyRunLoadScreen? DailyRunLoadScreen => Validate<NDailyRunLoadScreen>(ref _dailyRunLoadScreen);

	internal NMultiplayerSubmenu? MultiplayerSubmenu => Validate<NMultiplayerSubmenu>(ref _multiplayerSubmenu);

	internal NMultiplayerHostSubmenu? MultiplayerHostSubmenu => Validate<NMultiplayerHostSubmenu>(ref _multiplayerHostSubmenu);

	public override void _EnterTree()
	{
		((Node)this).Name = new StringName("SceneRegistry");
		Instance = this;
		SceneTree tree = ((Node)this).GetTree();
		tree.NodeAdded += new SceneTree.NodeAddedEventHandler(OnNodeAdded);
		IndexExistingTree((Node)(object)tree.Root);
	}

	public override void _ExitTree()
	{
		SceneTree tree = ((Node)this).GetTree();
		if (tree != null)
		{
			tree.NodeAdded -= new SceneTree.NodeAddedEventHandler(OnNodeAdded);
		}
		if (Instance == this)
		{
			Instance = null;
		}
	}

	private void OnNodeAdded(Node node)
	{
		Register(node);
	}

	private void IndexExistingTree(Node node)
	{
		Register(node);
		foreach (Node child in node.GetChildren(false))
		{
			IndexExistingTree(child);
		}
	}

	private void Register(Node node)
	{
		NSettingsScreen val = (NSettingsScreen)(object)((node is NSettingsScreen) ? node : null);
		if (val == null)
		{
			NRestSiteRoom val2 = (NRestSiteRoom)(object)((node is NRestSiteRoom) ? node : null);
			if (val2 == null)
			{
				NMerchantRoom val3 = (NMerchantRoom)(object)((node is NMerchantRoom) ? node : null);
				if (val3 == null)
				{
					NTreasureRoomRelicCollection val4 = (NTreasureRoomRelicCollection)(object)((node is NTreasureRoomRelicCollection) ? node : null);
					if (val4 == null)
					{
						NCharacterSelectScreen val5 = (NCharacterSelectScreen)(object)((node is NCharacterSelectScreen) ? node : null);
						if (val5 == null)
						{
							NCustomRunScreen val6 = (NCustomRunScreen)(object)((node is NCustomRunScreen) ? node : null);
							if (val6 == null)
							{
								NDailyRunScreen val7 = (NDailyRunScreen)(object)((node is NDailyRunScreen) ? node : null);
								if (val7 == null)
								{
									NMultiplayerLoadGameScreen val8 = (NMultiplayerLoadGameScreen)(object)((node is NMultiplayerLoadGameScreen) ? node : null);
									if (val8 == null)
									{
										NCustomRunLoadScreen val9 = (NCustomRunLoadScreen)(object)((node is NCustomRunLoadScreen) ? node : null);
										if (val9 == null)
										{
											NDailyRunLoadScreen val10 = (NDailyRunLoadScreen)(object)((node is NDailyRunLoadScreen) ? node : null);
											if (val10 == null)
											{
												NMultiplayerSubmenu val11 = (NMultiplayerSubmenu)(object)((node is NMultiplayerSubmenu) ? node : null);
												if (val11 == null)
												{
													NMultiplayerHostSubmenu val12 = (NMultiplayerHostSubmenu)(object)((node is NMultiplayerHostSubmenu) ? node : null);
													if (val12 != null)
													{
														_multiplayerHostSubmenu = val12;
													}
												}
												else
												{
													_multiplayerSubmenu = val11;
												}
											}
											else
											{
												_dailyRunLoadScreen = val10;
											}
										}
										else
										{
											_customRunLoadScreen = val9;
										}
									}
									else
									{
										_multiplayerLoadGameScreen = val8;
									}
								}
								else
								{
									_dailyRunScreen = val7;
								}
							}
							else
							{
								_customRunScreen = val6;
							}
						}
						else
						{
							_characterSelectScreen = val5;
						}
					}
					else
					{
						_treasureRoomRelicCollection = val4;
					}
				}
				else
				{
					_merchantRoom = val3;
				}
			}
			else
			{
				_restSiteRoom = val2;
			}
		}
		else
		{
			_settingsScreen = val;
		}
	}

	private static T? Validate<T>(ref T? node) where T : Node
	{
		if (node != null && GodotObject.IsInstanceValid((GodotObject)(object)node) && !((GodotObject)node/*cast due to constrained. prefix*/).IsQueuedForDeletion())
		{
			return node;
		}
		node = default(T);
		return default(T);
	}








}
