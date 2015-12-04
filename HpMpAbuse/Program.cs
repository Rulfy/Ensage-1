using System;
using System.Collections.Generic;
using System.Linq;
using Ensage;
using Ensage.Common;
using Ensage.Common.Extensions;
using Ensage.Common.Menu;
using Ensage.Items;
using Attribute = Ensage.Attribute;

namespace HpMpAbuse {
	internal class Program {

		private static bool stopAttack = true;
		private static bool ptChanged, healActive, disableSwitchBack, autoDisablePT, attacking, enabledRecovery;

		private static Attribute lastPtAttribute = Attribute.Strength;
		private static PowerTreads powerTreads;
		private static Hero hero;

		private static readonly string[] BonusHealth = {"bonus_strength", "bonus_all_stats", "bonus_health"};
		private static readonly string[] BonusMana = {"bonus_intellect", "bonus_all_stats", "bonus_mana"};

		private static readonly Menu Menu = new Menu("Smart HP/MP Abuse", "smartAbuse", true);

		private static readonly Menu PTMenu = new Menu("PT Switcher", "ptSwitcher");
		private static readonly Menu RecoveryMenu = new Menu("Recovery Abuse", "recoveryAbuse");
		private static readonly Menu SoulRingMenu = new Menu("Auto Soul Ring", "soulringAbuse");

		private static readonly Dictionary<string, bool> AbilitiesPT = new Dictionary<string, bool>();
		private static readonly Dictionary<string, bool> AbilitiesSR = new Dictionary<string, bool>();

		private static readonly string[] AttackSpells = {
			"windrunner_focusfire",
			"clinkz_searing_arrows",
			"silencer_glaives_of_wisdom",
			"templar_assassin_meld"
		};

		private static readonly string[] IgnoredSpells = {
			"item_tpscroll",
			"item_travel_boots",
			"item_travel_boots_2"
		};

		private static readonly string[] HealModifiers = {
			"modifier_item_urn_heal",
			"modifier_flask_healing",
			"modifier_bottle_regeneration",
			"modifier_voodoo_restoration_heal",
			"modifier_tango_heal",
			"modifier_enchantress_natures_attendants",
			"modifier_oracle_purifying_flames",
			"modifier_warlock_shadow_word",
			"modifier_treant_living_armor",
			"modifier_clarity_potion"
		};

		private static readonly string[] DisableSwitchBackModifiers = {
			"modifier_leshrac_pulse_nova",
			"modifier_morphling_morph_agi",
			"modifier_morphling_morph_str",
			"modifier_voodoo_restoration_aura",
			"modifier_brewmaster_primal_split",
			"modifier_eul_cyclone"
		};

		private static void Main() {
			Game.OnUpdate += Game_OnUpdate;
			Game.OnWndProc += Game_OnWndProc;
			Player.OnExecuteOrder += Player_OnExecuteAction;

			RecoveryMenu.AddItem(new MenuItem("hotkey", "Change hotkey").SetValue(new KeyBind('T', KeyBindType.Press)));

			var forcePick = new Menu("Force item picking", "forcePick");

			forcePick.AddItem(new MenuItem("forcePickMoved", "When hero moved").SetValue(true));
			forcePick.AddItem(new MenuItem("forcePickEnemyNear", "When enemy is near").SetValue(true));
			forcePick.AddItem(new MenuItem("forcePickEnemyNearDistance", "Enemy distance").SetValue(new Slider(500, 300, 1000))
				.SetTooltip("If enemy closer pick items when enabled"));

			RecoveryMenu.AddSubMenu(forcePick);

			PTMenu.AddItem(new MenuItem("enabledPT", "Enabled").SetValue(true));
			PTMenu.AddItem(new MenuItem("enabledPTAbilities", "Enabled for").SetValue(new AbilityToggler(AbilitiesPT)))
				.DontSave();
			PTMenu.AddItem(new MenuItem("switchPTonMove", "Switch when moving").SetValue(
				new StringList(new[] {"Don't switch", "Main attribute", "Strength", "Intelligence", "Agility"})))
				.SetTooltip("Switch PT to selected attribute when moving");
			PTMenu.AddItem(new MenuItem("switchPTonAttack", "Swtich when attacking").SetValue(
				new StringList(new[] {"Don't switch", "Main attribute", "Strength", "Intelligence", "Agility"})))
				.SetTooltip("Switch PT to selected attribute when attacking");
			PTMenu.AddItem(new MenuItem("switchPTHeal", "Swtich when healing").SetValue(true))
				.SetTooltip("Bottle, flask, clarity and other hero spells");
			PTMenu.AddItem(new MenuItem("manaPTThreshold", "Mana cost threshold").SetValue(new Slider(15, 0, 50))
				.SetTooltip("Don't switch PT if spell/item costs less mana"));
			PTMenu.AddItem(new MenuItem("switchbackPTdelay", "Switch back delay").SetValue(new Slider(500, 100, 1000))
				.SetTooltip("Make delay bigger if you have issues with PT when casting more than 1 spell in a row"));
			PTMenu.AddItem(new MenuItem("autoPTdisable", "Auto disable PT switcher").SetValue(new Slider(0, 0, 60))
				.SetTooltip("Auto disable PT switching after X min (always enabled: 0)"));

			SoulRingMenu.AddItem(new MenuItem("enabledSR", "Enabled").SetValue(true));
			SoulRingMenu.AddItem(new MenuItem("enabledSRAbilities", "Enabled for").SetValue(new AbilityToggler(AbilitiesSR)))
				.DontSave();
			SoulRingMenu.AddItem(new MenuItem("soulringHPThreshold", "HP threshold").SetValue(new Slider(30))
				.SetTooltip("Don't use soul ring if HP % less than X"));

			Menu.AddSubMenu(PTMenu);
			Menu.AddSubMenu(RecoveryMenu);
			Menu.AddSubMenu(SoulRingMenu);

			Menu.AddItem(new MenuItem("checkPTdelay", "PT check delay").SetValue(new Slider(250, 200, 500))
				.SetTooltip("Make delay bigger if PT constantly switching when using bottle for example"));

			Menu.AddToMainMenu();
		}

		private static void Game_OnWndProc(WndEventArgs args) {
			if (args.WParam == RecoveryMenu.Item("hotkey").GetValue<KeyBind>().Key && !Game.IsChatOpen) {
				enabledRecovery = args.Msg == (uint) Utils.WindowsMessages.WM_KEYDOWN;

				if (stopAttack) {
					Game.ExecuteCommand("dota_player_units_auto_attack_after_spell 0");
					stopAttack = false;
				}

				if (!enabledRecovery) {
					PickUpItems();
					Game.ExecuteCommand("dota_player_units_auto_attack_after_spell 1");
					stopAttack = true;
				}
			}
		}

		private static void Player_OnExecuteAction(Player sender, ExecuteOrderEventArgs args) {
			switch (args.Order) {
				case Order.AttackTarget:
				case Order.AttackLocation:
					ChangePtOnAction("switchPTonAttack", true);
					break;
				case Order.AbilityTarget:
				case Order.AbilityLocation:
				case Order.Ability:
				case Order.ToggleAbility:
					if (!Game.IsKeyDown(16))
						CastSpell(args);
					break;
				case Order.MoveItem:
				case Order.MoveLocation:
				case Order.MoveTarget:
					ChangePtOnAction("switchPTonMove");
					break;
				default:
					attacking = false;
					break;
			}
		}

		private static void Game_OnUpdate(EventArgs args) {

			if (!Utils.SleepCheck("delay"))
				return;

			if (!Game.IsInGame) {
				if (AbilitiesPT.Count > 0) {
					AbilitiesPT.Clear();
					AbilitiesSR.Clear();
					hero = null;
				}
				if (autoDisablePT) {
					PTMenu.Item("enabledPT").SetValue(false).DontSave();
					autoDisablePT = false;
				}
				Utils.Sleep(1111, "delay");
				return;
			}

			if (hero == null)
				hero = ObjectMgr.LocalHero;

			if (hero == null || !hero.IsAlive || Game.IsPaused) {
				Utils.Sleep(555, "delay");
				return;
			}

			var reloadMenu = false;

			foreach (var spell in hero.Spellbook.Spells.Where(CheckAbility)) {
				AbilitiesPT.Add(spell.Name, true);
				AbilitiesSR.Add(spell.Name, true);
				reloadMenu = true;
			}

			foreach (var item in hero.Inventory.Items.Where(CheckAbility)) {
				AbilitiesPT.Add(item.Name, true);
				AbilitiesSR.Add(item.Name, true);
				reloadMenu = true;
			}

			if (reloadMenu) {
				PTMenu.Item("enabledPTAbilities").SetValue((new AbilityToggler(AbilitiesPT))).DontSave();
				SoulRingMenu.Item("enabledSRAbilities").SetValue((new AbilityToggler(AbilitiesSR))).DontSave();
			}

			if (!autoDisablePT && Game.GameTime / 60 > PTMenu.Item("autoPTdisable").GetValue<Slider>().Value &&
			    PTMenu.Item("autoPTdisable").GetValue<Slider>().Value != 0 && PTMenu.Item("enabledPT").GetValue<bool>()) {
				PTMenu.Item("enabledPT").SetValue(false).DontSave();
				autoDisablePT = true;
			}

			if (autoDisablePT && PTMenu.Item("enabledPT").GetValue<bool>()) {
				autoDisablePT = false;
			}

			powerTreads = (PowerTreads) hero.FindItem("item_power_treads");

			if (powerTreads != null && !ptChanged && !attacking) {
				switch (powerTreads.ActiveAttribute) {
					case Attribute.Intelligence: // agi
						lastPtAttribute = Attribute.Agility;
						break;
					case Attribute.Strength:
						lastPtAttribute = Attribute.Strength;
						break;
					case Attribute.Agility: // int
						lastPtAttribute = Attribute.Intelligence;
						break;
				}
			}

			if (attacking)
				ChangePtOnAction("switchPTonAttack", true);

			if (enabledRecovery && (hero.Mana < hero.MaximumMana || hero.Health < hero.MaximumHealth)) {

				if (hero.NetworkActivity == NetworkActivity.Move && RecoveryMenu.Item("forcePickMoved").GetValue<bool>()) {
					PickUpItems(true);
					Utils.Sleep(1000, "delay");
					return;
				}

				if (ObjectMgr.GetEntities<Hero>().Any(x =>
					x.IsAlive && x.IsVisible && x.Team == hero.GetEnemyTeam() &&
					x.Distance2D(hero) <= RecoveryMenu.Item("forcePickEnemyNearDistance").GetValue<Slider>().Value) &&
				    RecoveryMenu.Item("forcePickEnemyNear").GetValue<bool>()) {
					PickUpItems();
					Utils.Sleep(1000, "delay");
					return;
				}

				var arcaneBoots = hero.FindItem("item_arcane_boots");
				var greaves = hero.FindItem("item_guardian_greaves");
				var soulRing = hero.FindItem("item_soul_ring");
				var bottle = hero.FindItem("item_bottle");
				var stick = hero.FindItem("item_magic_stick") ?? hero.FindItem("item_magic_wand");
				var meka = hero.FindItem("item_mekansm");
				var urn = hero.FindItem("item_urn_of_shadows");

				if (meka != null && meka.CanBeCasted() && hero.Health != hero.MaximumHealth) {
					ChangePowerTreads(Attribute.Intelligence);
					DropItems(BonusHealth, meka);
					meka.UseAbility(true);
				}

				if (arcaneBoots != null && arcaneBoots.CanBeCasted() && hero.Mana < hero.MaximumMana) {
					ChangePowerTreads(Attribute.Agility);
					DropItems(BonusMana, arcaneBoots);
					arcaneBoots.UseAbility(true);
				}

				if (greaves != null && greaves.CanBeCasted()) {
					ChangePowerTreads(Attribute.Agility);
					DropItems(BonusHealth.Concat(BonusMana), greaves);
					greaves.UseAbility(true);
				}

				if (soulRing != null && soulRing.CanBeCasted()) {
					ChangePowerTreads(Attribute.Strength);
					DropItems(BonusMana);
					soulRing.UseAbility(true);
				}

				if (bottle != null && bottle.CanBeCasted() && bottle.CurrentCharges != 0 &&
				    hero.Modifiers.All(x => x.Name != "modifier_bottle_regeneration")) {

					if ((float) hero.Health / hero.MaximumHealth < 0.9)
						DropItems(BonusHealth);
					if (hero.Mana / hero.MaximumMana < 0.9)
						DropItems(BonusMana);

					bottle.UseAbility(true);
				}

				if (stick != null && stick.CanBeCasted() && stick.CurrentCharges != 0) {
					ChangePowerTreads(Attribute.Agility);

					if ((float) hero.Health / hero.MaximumHealth < 0.9)
						DropItems(BonusHealth, stick);
					if (hero.Mana / hero.MaximumMana < 0.9)
						DropItems(BonusMana, stick);

					stick.UseAbility(true);
				}

				if (urn != null && urn.CanBeCasted() && urn.CurrentCharges != 0 &&
				    hero.Modifiers.All(x => x.Name != "modifier_item_urn_heal") && (float) hero.Health / hero.MaximumHealth < 0.9) {
					DropItems(BonusHealth, urn);
					urn.UseAbility(hero, true);
				}

				if (hero.Modifiers.Any(x => HealModifiers.Any(x.Name.Contains))) {
					if ((float) hero.Health / hero.MaximumHealth < 0.9)
						DropItems(BonusHealth);
					if (hero.Modifiers.Any(x => x.Name == "modifier_bottle_regeneration")) {
						if (hero.Mana / hero.MaximumMana < 0.9)
							DropItems(BonusMana);
					}
				}

				var allies = ObjectMgr.GetEntities<Hero>().Where(x => x.Distance2D(hero) <= 900 && x.IsAlive && x.Team == hero.Team);

				foreach (var ally in allies) {
					var allyArcaneBoots = ally.FindItem("item_arcane_boots");
					var allyMeka = ally.FindItem("item_mekansm");
					var allyGreaves = ally.FindItem("item_guardian_greaves");

					if (allyArcaneBoots != null && allyArcaneBoots.AbilityState == AbilityState.Ready) {
						ChangePowerTreads(Attribute.Agility);
						DropItems(BonusMana);
					}

					if (allyMeka != null && allyMeka.AbilityState == AbilityState.Ready) {
						ChangePowerTreads(Attribute.Agility);
						DropItems(BonusHealth);
					}

					if (allyGreaves != null && allyGreaves.AbilityState == AbilityState.Ready) {
						ChangePowerTreads(Attribute.Agility);
						DropItems(BonusMana.Concat(BonusHealth));
					}
				}
			}

			if (powerTreads == null) {
				Utils.Sleep(Menu.Item("checkPTdelay").GetValue<Slider>().Value, "delay");
				return;
			}

			disableSwitchBack = hero.Modifiers.Any(x => DisableSwitchBackModifiers.Any(x.Name.Contains));

			if (hero.Modifiers.Any(x => HealModifiers.Any(x.Name.Contains)) && !disableSwitchBack &&
			    (PTMenu.Item("switchPTHeal").GetValue<bool>() && PTMenu.Item("enabledPT").GetValue<bool>() || enabledRecovery)) {

				if (hero.Modifiers.Any(x => (x.Name == "modifier_bottle_regeneration" || x.Name == "modifier_clarity_potion"))) {
					if (hero.Mana / hero.MaximumMana < 0.9 && (float) hero.Health / hero.MaximumHealth > 0.9) {
						if (lastPtAttribute == Attribute.Intelligence) {
							ChangePowerTreads(Attribute.Strength, true, true);
						} else {
							healActive = false;
						}
					} else if (hero.Mana / hero.MaximumMana > 0.9 && (float) hero.Health / hero.MaximumHealth < 0.9) {
						if (lastPtAttribute == Attribute.Strength) {
							if (hero.PrimaryAttribute == Attribute.Agility)
								ChangePowerTreads(Attribute.Agility, true, true);
							else if (hero.PrimaryAttribute == Attribute.Intelligence)
								ChangePowerTreads(Attribute.Intelligence, true, true);
						} else {
							healActive = false;
						}
					} else if (hero.Mana / hero.MaximumMana < 0.9 && (float) hero.Health / hero.MaximumHealth < 0.9) {
						ChangePowerTreads(Attribute.Agility, true, true);
					} else {
						healActive = false;
					}
				} else {
					if ((float) hero.Health / hero.MaximumHealth < 0.9) {
						if (lastPtAttribute == Attribute.Strength) {
							if (hero.PrimaryAttribute == Attribute.Agility)
								ChangePowerTreads(Attribute.Agility, true, true);
							else if (hero.PrimaryAttribute == Attribute.Intelligence)
								ChangePowerTreads(Attribute.Intelligence, true, true);
						} else {
							healActive = false;
						}
					} else if (hero.Health == hero.MaximumHealth && healActive) {
						healActive = false;
					}
				}
			} else {
				healActive = false;
			}

			if (ptChanged && !healActive && !disableSwitchBack && !enabledRecovery && !attacking) {

				foreach (var spell in hero.Spellbook.Spells.Where(spell => spell.IsInAbilityPhase)) {
					Utils.Sleep(spell.FindCastPoint() * 1000 + PTMenu.Item("switchbackPTdelay").GetValue<Slider>().Value, "delay");
					return;
				}

				ChangePowerTreads(lastPtAttribute, false);
			}

			Utils.Sleep(Menu.Item("checkPTdelay").GetValue<Slider>().Value, "delay");
		}

		private static void CastSpell(ExecuteOrderEventArgs args) {

			var spell = args.Ability;

			if (spell.ManaCost <= PTMenu.Item("manaPTThreshold").GetValue<Slider>().Value ||
			    IgnoredSpells.Any(spell.Name.Contains))
				return;

			var soulRing = hero.FindItem("item_soul_ring");

			if (powerTreads == null && soulRing == null)
				return;

			if (!PTMenu.Item("enabledPT").GetValue<bool>() && !SoulRingMenu.Item("enabledSR").GetValue<bool>())
				return;

			args.Process = false;

			if (SoulRingMenu.Item("enabledSR").GetValue<bool>()) {
				if (soulRing != null && soulRing.CanBeCasted() &&
				    SoulRingMenu.Item("enabledSRAbilities").GetValue<AbilityToggler>().IsEnabled(spell.Name) &&
				    ((float) hero.Health / hero.MaximumHealth) * 100 >=
				    SoulRingMenu.Item("soulringHPThreshold").GetValue<Slider>().Value) {
					soulRing.UseAbility();
				}
			}

			var sleep = spell.FindCastPoint() * 1000 + PTMenu.Item("switchbackPTdelay").GetValue<Slider>().Value;

			if (AttackSpells.Any(spell.Name.Contains))
				sleep += hero.SecondsPerAttack * 1000;

			switch (args.Order) {
				case Order.AbilityTarget: {
					var target = (Unit) args.Target;
					if (target != null && target.IsAlive) {

						var castRange = spell.GetCastRange() + 300;

						if (hero.Distance2D(target) <= castRange && PTMenu.Item("enabledPT").GetValue<bool>()) {
							if (PTMenu.Item("enabledPTAbilities").GetValue<AbilityToggler>().IsEnabled(spell.Name))
								ChangePowerTreads(Attribute.Intelligence);
							else if (AttackSpells.Any(spell.Name.Contains)) {
								ChangePtOnAction("switchPTonAttack", true);
							}
							sleep += hero.GetTurnTime(target) * 1000;
						}
						spell.UseAbility(target);
					}
					break;
				}
				case Order.AbilityLocation: {
					var castRange = spell.GetCastRange() + 300;

					if (hero.Distance2D(Game.MousePosition) <= castRange && PTMenu.Item("enabledPT").GetValue<bool>() &&
					    PTMenu.Item("enabledPTAbilities").GetValue<AbilityToggler>().IsEnabled(spell.Name)) {
						ChangePowerTreads(Attribute.Intelligence);
						sleep += hero.GetTurnTime(Game.MousePosition) * 1000;
					}
					spell.UseAbility(Game.MousePosition);
					break;
				}
				case Order.Ability: {
					if (PTMenu.Item("enabledPT").GetValue<bool>()) {
						if (PTMenu.Item("enabledPTAbilities").GetValue<AbilityToggler>().IsEnabled(spell.Name))
							ChangePowerTreads(Attribute.Intelligence);
						else if (spell.Name == AttackSpells[3]) {
							ChangePtOnAction("switchPTonAttack", true);
						}
					}
					spell.UseAbility();
					break;
				}
				case Order.ToggleAbility: {
					if (PTMenu.Item("enabledPT").GetValue<bool>() &&
					    PTMenu.Item("enabledPTAbilities").GetValue<AbilityToggler>().IsEnabled(spell.Name))
						ChangePowerTreads(Attribute.Intelligence);
					spell.ToggleAbility();
					break;
				}
			}
			Utils.Sleep(sleep, "delay");
		}

		private static void ChangePowerTreads(Attribute attribute, bool switchBack = true, bool healing = false) {
			if (powerTreads == null)
				return;

			healActive = healing;

			if (hero.IsChanneling() || hero.IsInvisible() || !hero.CanUseItems())
				return;

			var ptNow = 0;
			var ptTo = 0;

			switch (powerTreads.ActiveAttribute) {
				case Attribute.Intelligence: // agi
					ptNow = 3;
					break;
				case Attribute.Strength:
					ptNow = 1;
					break;
				case Attribute.Agility: // int
					ptNow = 2;
					break;
			}

			switch (attribute) {
				case Attribute.Intelligence:
					ptTo = 2;
					break;
				case Attribute.Strength:
					ptTo = 1;
					break;
				case Attribute.Agility:
					ptTo = 3;
					break;
			}

			if (ptNow == ptTo)
				return;

			var change = ptTo - ptNow % 3;

			if (ptNow == 2 && ptTo == 1) // random fix
				change = 2;

			ptChanged = switchBack;

			for (var i = 0; i < change; i++)
				powerTreads.UseAbility();
		}

		private static void ChangePtOnAction(string action, bool isAttacking = false) {

			if (!PTMenu.Item("enabledPT").GetValue<bool>() || healActive || enabledRecovery)
				return;

			switch (PTMenu.Item(action).GetValue<StringList>().SelectedIndex) {
				case 1:
					ChangePowerTreads(hero.PrimaryAttribute, false);
					break;
				case 2:
					ChangePowerTreads(Attribute.Strength, false);
					break;
				case 3:
					ChangePowerTreads(Attribute.Intelligence, false);
					break;
				case 4:
					ChangePowerTreads(Attribute.Agility, false);
					break;
				default:
					return;
			}
			attacking = isAttacking;
			Utils.Sleep(500, "delay");
		}

		private static void PickUpItems(bool moving = false) {

			if (moving)
				hero.Stop();

			var droppedItems = ObjectMgr.GetEntities<PhysicalItem>().Where(x => x.Distance2D(hero) < 250).ToList();

			for (var i = 0; i < droppedItems.Count; i++)
				hero.PickUpItem(droppedItems[i], i != 0);

			if (moving)
				hero.Move(Game.MousePosition, true);

			if (!ptChanged)
				return;

			ChangePowerTreads(lastPtAttribute, false);
		}

		private static void DropItems(IEnumerable<string> bonusStats, Item ignoredItem = null) {
			var items = hero.Inventory.Items;

			foreach (var item in
					items.Where(item => !item.Equals(ignoredItem) && item.AbilityData.Any(x => bonusStats.Any(x.Name.Contains))))
				hero.DropItem(item, hero.NetworkPosition, true);
		}

		private static bool CheckAbility(Ability ability) {
			return ability.ManaCost > 0 && !AbilitiesPT.ContainsKey(ability.Name) && !IgnoredSpells.Any(ability.Name.Contains) &&
			       ability.AbilityBehavior != AbilityBehavior.Passive;
		}
	}
}