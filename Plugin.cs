using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace HideArmor
{
    public enum FilterMode
    {
        UseSlotRules = 0,
        WhitelistOnly = 1,
        Blacklist = 2
    }

    public enum ItemOption
    {
        Default = 0,
        AlwaysHide = 1,
        AlwaysShow = 2
    }

    [BepInPlugin("com.furyg.hidearmor", "Hide Armor", "1.2.0")]
    public class HideArmorPlugin : BaseUnityPlugin
    {
        public static HideArmorPlugin Instance { get; private set; }
        public static ManualLogSource Log => Instance?.Logger;

        // Section 1 - General Settings
        public static ConfigEntry<bool> ModEnabled { get; private set; }
        public static ConfigEntry<KeyboardShortcut> ToggleHotkey { get; private set; }
        public static ConfigEntry<bool> HideForPlayer { get; private set; }
        public static ConfigEntry<bool> HideInMenu { get; private set; }
        public static ConfigEntry<bool> HideForBots { get; private set; }

        // Section 2 - Equipment Slots
        public static ConfigEntry<bool> HideArmorVest { get; private set; }
        public static ConfigEntry<bool> HideArmoredChestRigs { get; private set; }
        public static ConfigEntry<bool> HideNonArmoredChestRigs { get; private set; }
        public static ConfigEntry<bool> HideArmoredHeadwear { get; private set; }
        public static ConfigEntry<bool> HideNonArmoredHeadwear { get; private set; }
        public static ConfigEntry<bool> HideBackpacks { get; private set; }
        public static ConfigEntry<bool> HideFaceCovers { get; private set; }
        public static ConfigEntry<bool> HideEyewear { get; private set; }
        public static ConfigEntry<bool> HideArmBands { get; private set; }
        public static ConfigEntry<bool> HideHolsters { get; private set; }
        public static ConfigEntry<bool> HideEarpieces { get; private set; }

        // Section 3 - Name & ID Filtering
        public static ConfigEntry<FilterMode> CurrentFilterMode { get; private set; }
        public static ConfigEntry<string> ItemsToHide { get; private set; }
        public static ConfigEntry<string> ItemsToKeepVisible { get; private set; }
        public static ConfigEntry<bool> LogEquippedItems { get; private set; }

        // Section 4 - Individual Item Overrides
        public static ConfigEntry<string> Item1_NameOrId { get; private set; }
        public static ConfigEntry<ItemOption> Item1_Option { get; private set; }

        public static ConfigEntry<string> Item2_NameOrId { get; private set; }
        public static ConfigEntry<ItemOption> Item2_Option { get; private set; }

        public static ConfigEntry<string> Item3_NameOrId { get; private set; }
        public static ConfigEntry<ItemOption> Item3_Option { get; private set; }

        public static ConfigEntry<string> Item4_NameOrId { get; private set; }
        public static ConfigEntry<ItemOption> Item4_Option { get; private set; }

        public static ConfigEntry<string> Item5_NameOrId { get; private set; }
        public static ConfigEntry<ItemOption> Item5_Option { get; private set; }

        private static readonly FieldInfo IsYourPlayerField = typeof(PlayerBody).GetField("_isYourPlayer", BindingFlags.Instance | BindingFlags.NonPublic);

        private void Awake()
        {
            Instance = this;

            InitConfig();

            try
            {
                var harmony = new Harmony("com.furyg.hidearmor");
                harmony.PatchAll(typeof(SlotViewPatches));
                harmony.PatchAll(typeof(PlayerBodyPatches));
                Logger.LogInfo("Hide Armor mod loaded successfully!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to patch EFT methods: {ex}");
            }
        }

        private void InitConfig()
        {
            const string secGeneral = "1. General Settings";
            ModEnabled = Config.Bind(secGeneral, "Enabled", true, "Master toggle to enable or disable the Hide Armor mod.");
            ToggleHotkey = Config.Bind(secGeneral, "Toggle Hotkey", new KeyboardShortcut(KeyCode.None), "Optional keybind to quickly toggle armor visibility in-raid.");
            HideForPlayer = Config.Bind(secGeneral, "Hide For Local Player", true, "Hide armor on your character in-raid (both first person and third person).");
            HideInMenu = Config.Bind(secGeneral, "Hide In Menu / Inventory", true, "Hide armor on your character in the main menu and inventory screen.");
            HideForBots = Config.Bind(secGeneral, "Hide For Bots", false, "Hide armor on AI scavs, PMCs, and bosses (default: false).");

            const string secSlots = "2. Equipment Slots";
            HideArmorVest = Config.Bind(secSlots, "Hide Armor Vest", true, "Hide body armor in the ArmorVest slot (e.g. Slick, Korund, PACA, Trooper).");
            HideArmoredChestRigs = Config.Bind(secSlots, "Hide Armored Chest Rigs", true, "Hide chest rigs in the TacticalVest slot ONLY if they provide armor (e.g. AVS, TV-110, CPC, Thor CR).");
            HideNonArmoredChestRigs = Config.Bind(secSlots, "Hide Non-Armored Chest Rigs", false, "Hide chest rigs in the TacticalVest slot even if they are unarmored (e.g. Bank Robber, Triton, Blackrock).");
            HideArmoredHeadwear = Config.Bind(secSlots, "Hide Armored Headwear (Helmets)", false, "Hide helmets and armored headgear in the Headwear slot (e.g. Altyn, Fast MT, Team Wendy).");
            HideNonArmoredHeadwear = Config.Bind(secSlots, "Hide Non-Armored Headwear (Hats/Caps)", false, "Hide non-armored headwear in the Headwear slot (e.g. baseball caps, beanies, berets).");
            HideBackpacks = Config.Bind(secSlots, "Hide Backpacks", false, "Hide backpacks in the Backpack slot.");
            HideFaceCovers = Config.Bind(secSlots, "Hide Face Covers", false, "Hide balaclavas, masks, and shemaghs in the FaceCover slot.");
            HideEyewear = Config.Bind(secSlots, "Hide Eyewear", false, "Hide glasses and goggles in the Eyewear slot.");
            HideArmBands = Config.Bind(secSlots, "Hide Armbands", false, "Hide armbands in the ArmBand slot.");
            HideHolsters = Config.Bind(secSlots, "Hide Holsters", false, "Hide sidearm holsters in the Holster slot.");
            HideEarpieces = Config.Bind(secSlots, "Hide Earpieces (Headsets)", false, "Hide headsets and earpieces in the Earpiece slot (e.g. ComTac 2, Sordin, Razor).");

            const string secFilter = "3. Name and ID Filtering";
            CurrentFilterMode = Config.Bind(secFilter, "Filter Mode", FilterMode.UseSlotRules,
                "How name/ID filtering operates:\n" +
                "- UseSlotRules: Uses slot rules above, plus individual item overrides.\n" +
                "- WhitelistOnly: ONLY hides items specified in the lists or set to AlwaysHide.\n" +
                "- Blacklist: Hides items matching slot rules, EXCEPT those specified to stay visible.");
            ItemsToHide = Config.Bind(secFilter, "Items To Hide (List)", "",
                "Comma-separated list of item names, short names, or template IDs to ALWAYS HIDE (e.g. 'slick, altyn, 5b47574386f77428ca22b2f4'). Case-insensitive.");
            ItemsToKeepVisible = Config.Bind(secFilter, "Items To Keep Visible (List)", "",
                "Comma-separated list of item names, short names, or template IDs to NEVER HIDE (e.g. 'paca, untar'). Case-insensitive.");
            LogEquippedItems = Config.Bind(secFilter, "Log Equipped Item Info", true,
                "When you equip an item in menu or raid, its exact Name, ShortName, and Template ID are printed to the BepInEx log so you can easily copy them.");

            const string secOverrides = "4. Individual Item Overrides";
            Item1_NameOrId = Config.Bind(secOverrides, "Item 1: Name or ID", "", "Name, short name, or template ID (e.g. 'slick')");
            Item1_Option = Config.Bind(secOverrides, "Item 1: Option", ItemOption.Default, "Action: Default (follow slot rules), AlwaysHide, or AlwaysShow");

            Item2_NameOrId = Config.Bind(secOverrides, "Item 2: Name or ID", "", "Name, short name, or template ID (e.g. 'altyn')");
            Item2_Option = Config.Bind(secOverrides, "Item 2: Option", ItemOption.Default, "Action: Default (follow slot rules), AlwaysHide, or AlwaysShow");

            Item3_NameOrId = Config.Bind(secOverrides, "Item 3: Name or ID", "", "Name, short name, or template ID (e.g. 'korund')");
            Item3_Option = Config.Bind(secOverrides, "Item 3: Option", ItemOption.Default, "Action: Default (follow slot rules), AlwaysHide, or AlwaysShow");

            Item4_NameOrId = Config.Bind(secOverrides, "Item 4: Name or ID", "", "Name, short name, or template ID");
            Item4_Option = Config.Bind(secOverrides, "Item 4: Option", ItemOption.Default, "Action: Default (follow slot rules), AlwaysHide, or AlwaysShow");

            Item5_NameOrId = Config.Bind(secOverrides, "Item 5: Name or ID", "", "Name, short name, or template ID");
            Item5_Option = Config.Bind(secOverrides, "Item 5: Option", ItemOption.Default, "Action: Default (follow slot rules), AlwaysHide, or AlwaysShow");

            // Register setting change listeners to apply changes immediately
            ModEnabled.SettingChanged += OnConfigChanged;
            HideForPlayer.SettingChanged += OnConfigChanged;
            HideInMenu.SettingChanged += OnConfigChanged;
            HideForBots.SettingChanged += OnConfigChanged;
            HideArmorVest.SettingChanged += OnConfigChanged;
            HideArmoredChestRigs.SettingChanged += OnConfigChanged;
            HideNonArmoredChestRigs.SettingChanged += OnConfigChanged;
            HideArmoredHeadwear.SettingChanged += OnConfigChanged;
            HideNonArmoredHeadwear.SettingChanged += OnConfigChanged;
            HideBackpacks.SettingChanged += OnConfigChanged;
            HideFaceCovers.SettingChanged += OnConfigChanged;
            HideEyewear.SettingChanged += OnConfigChanged;
            HideArmBands.SettingChanged += OnConfigChanged;
            HideHolsters.SettingChanged += OnConfigChanged;
            HideEarpieces.SettingChanged += OnConfigChanged;

            CurrentFilterMode.SettingChanged += OnConfigChanged;
            ItemsToHide.SettingChanged += OnConfigChanged;
            ItemsToKeepVisible.SettingChanged += OnConfigChanged;
            Item1_NameOrId.SettingChanged += OnConfigChanged;
            Item1_Option.SettingChanged += OnConfigChanged;
            Item2_NameOrId.SettingChanged += OnConfigChanged;
            Item2_Option.SettingChanged += OnConfigChanged;
            Item3_NameOrId.SettingChanged += OnConfigChanged;
            Item3_Option.SettingChanged += OnConfigChanged;
            Item4_NameOrId.SettingChanged += OnConfigChanged;
            Item4_Option.SettingChanged += OnConfigChanged;
            Item5_NameOrId.SettingChanged += OnConfigChanged;
            Item5_Option.SettingChanged += OnConfigChanged;
        }

        private void OnConfigChanged(object sender, EventArgs e)
        {
            ApplyToAllActiveBodies();
        }

        private void Update()
        {
            if (ToggleHotkey != null && ToggleHotkey.Value.IsDown())
            {
                ModEnabled.Value = !ModEnabled.Value;
                Log?.LogInfo($"Hide Armor toggled: {(ModEnabled.Value ? "ENABLED" : "DISABLED")}");
                ApplyToAllActiveBodies();
            }
        }

        public static bool CheckIsYourPlayer(PlayerBody playerBody)
        {
            if (playerBody == null) return false;

            var player = playerBody.GetComponentInParent<Player>();
            if (player != null) return player.IsYourPlayer;

            if (playerBody.GetComponentInParent<MenuPlayerPoser>() != null) return true;

            if (IsYourPlayerField != null)
            {
                try
                {
                    return (bool)IsYourPlayerField.GetValue(playerBody);
                }
                catch { }
            }

            return false;
        }

        public static bool CheckIsMenuPlayer(PlayerBody playerBody)
        {
            if (playerBody == null) return false;
            return playerBody.GetComponentInParent<MenuPlayerPoser>() != null || playerBody.GetComponentInParent<Player>() == null;
        }

        public static bool IsArmoredItem(Item item)
        {
            if (item == null) return false;

            if (item.Template is ArmoredEquipmentTemplate armoredTemplate)
            {
                if (armoredTemplate.armorClass > 0) return true;
                if (armoredTemplate.armorColliders != null && armoredTemplate.armorColliders.Length > 0) return true;
                if (armoredTemplate.armorPlateColliders != null && armoredTemplate.armorPlateColliders.Length > 0) return true;
            }

            if (item.Template is VestTemplate vestTemplate)
            {
                if (vestTemplate.armorClass > 0) return true;
                if (vestTemplate.armorPlateColliders != null && vestTemplate.armorPlateColliders.Length > 0) return true;
                if (vestTemplate.armorZone != null && vestTemplate.armorZone.Length > 0) return true;
            }

            if (item.GetItemComponent<ArmorComponent>() != null) return true;
            if (item.GetItemComponent<ArmorHolderComponent>() != null) return true;

            return false;
        }

        public static bool MatchesItem(Item item, string pattern)
        {
            if (item == null || string.IsNullOrWhiteSpace(pattern)) return false;

            pattern = pattern.Trim();
            if (pattern.Length == 0) return false;

            // 1. Direct ID comparison
            string templateId = item.StringTemplateId ?? item.TemplateId.ToString();
            if (!string.IsNullOrEmpty(templateId) && templateId.Equals(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 2. Check internal template Name and ShortName
            if (item.Template != null)
            {
                if (!string.IsNullOrEmpty(item.Template.Name) &&
                    item.Template.Name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(item.Template.ShortName) &&
                    item.Template.ShortName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            // 3. Check localized Name and ShortName
            try
            {
                string locName = item.Name.Localized();
                if (!string.IsNullOrEmpty(locName) &&
                    locName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                string locShortName = item.ShortName.Localized();
                if (!string.IsNullOrEmpty(locShortName) &&
                    locShortName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        public static bool ShouldHideSlot(EquipmentSlot slot, Item item, PlayerBody playerBody)
        {
            if (!ModEnabled.Value) return false;
            if (playerBody == null) return false;

            bool isMenu = CheckIsMenuPlayer(playerBody);
            bool isYourPlayer = CheckIsYourPlayer(playerBody);

            if (isMenu)
            {
                if (!HideInMenu.Value) return false;
            }
            else
            {
                if (isYourPlayer)
                {
                    if (!HideForPlayer.Value) return false;
                }
                else
                {
                    if (!HideForBots.Value) return false;
                }
            }

            if (item == null) return false;

            // Check individual item overrides (Slots 1 to 5)
            if (CheckIndividualOverride(item, Item1_NameOrId.Value, Item1_Option.Value, out bool overrideResult1))
                return overrideResult1;
            if (CheckIndividualOverride(item, Item2_NameOrId.Value, Item2_Option.Value, out bool overrideResult2))
                return overrideResult2;
            if (CheckIndividualOverride(item, Item3_NameOrId.Value, Item3_Option.Value, out bool overrideResult3))
                return overrideResult3;
            if (CheckIndividualOverride(item, Item4_NameOrId.Value, Item4_Option.Value, out bool overrideResult4))
                return overrideResult4;
            if (CheckIndividualOverride(item, Item5_NameOrId.Value, Item5_Option.Value, out bool overrideResult5))
                return overrideResult5;

            // Check custom list of items to keep visible (highest priority blacklist)
            if (!string.IsNullOrWhiteSpace(ItemsToKeepVisible.Value))
            {
                var visiblePatterns = ItemsToKeepVisible.Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < visiblePatterns.Length; i++)
                {
                    if (MatchesItem(item, visiblePatterns[i]))
                    {
                        return false;
                    }
                }
            }

            // Check custom list of items to hide (highest priority whitelist)
            if (!string.IsNullOrWhiteSpace(ItemsToHide.Value))
            {
                var hidePatterns = ItemsToHide.Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < hidePatterns.Length; i++)
                {
                    if (MatchesItem(item, hidePatterns[i]))
                    {
                        return true;
                    }
                }
            }

            // If WhitelistOnly mode is active, any item not explicitly matched above stays visible
            if (CurrentFilterMode.Value == FilterMode.WhitelistOnly)
            {
                return false;
            }

            // Default: check slot-specific rules
            switch (slot)
            {
                case EquipmentSlot.ArmorVest:
                    return HideArmorVest.Value;

                case EquipmentSlot.TacticalVest:
                    bool isArmoredRig = IsArmoredItem(item);
                    return isArmoredRig ? HideArmoredChestRigs.Value : HideNonArmoredChestRigs.Value;

                case EquipmentSlot.Headwear:
                    bool isArmoredHeadwear = IsArmoredItem(item);
                    return isArmoredHeadwear ? HideArmoredHeadwear.Value : HideNonArmoredHeadwear.Value;

                case EquipmentSlot.Backpack:
                    return HideBackpacks.Value;

                case EquipmentSlot.FaceCover:
                    return HideFaceCovers.Value;

                case EquipmentSlot.Eyewear:
                    return HideEyewear.Value;

                case EquipmentSlot.ArmBand:
                    return HideArmBands.Value;

                case EquipmentSlot.Holster:
                    return HideHolsters.Value;

                case EquipmentSlot.Earpiece:
                    return HideEarpieces.Value;

                default:
                    return false;
            }
        }

        private static bool CheckIndividualOverride(Item item, string targetPattern, ItemOption option, out bool result)
        {
            result = false;
            if (option == ItemOption.Default || string.IsNullOrWhiteSpace(targetPattern))
            {
                return false;
            }

            if (MatchesItem(item, targetPattern))
            {
                result = (option == ItemOption.AlwaysHide);
                return true;
            }

            return false;
        }

        public static void SetSlotRenderersEnabled(PlayerBody.SlotView slotView, bool enabled)
        {
            if (slotView == null) return;

            // 1. Direct slot renderers
            var renderers = slotView.Renderers;
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r != null && r.enabled != enabled)
                    {
                        r.enabled = enabled;
                    }
                }
            }

            // 2. Dresses (e.g. skinned armor/clothing)
            if (slotView.Dresses != null)
            {
                for (int i = 0; i < slotView.Dresses.Length; i++)
                {
                    var dress = slotView.Dresses[i];
                    if (dress == null) continue;

                    if (dress.Renderers != null)
                    {
                        for (int j = 0; j < dress.Renderers.Length; j++)
                        {
                            var r = dress.Renderers[j];
                            if (r != null && r.enabled != enabled)
                            {
                                r.enabled = enabled;
                            }
                        }
                    }

                    var enumRenderers = dress.GetRenderers();
                    if (enumRenderers != null)
                    {
                        foreach (var r in enumRenderers)
                        {
                            if (r != null && r.enabled != enabled)
                            {
                                r.enabled = enabled;
                            }
                        }
                    }
                }
            }

            // 3. Any additional child renderers in Model hierarchy (plates, attachments, pouches)
            var model = slotView.Model;
            if (model != null)
            {
                var allRenderers = model.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < allRenderers.Length; i++)
                {
                    var r = allRenderers[i];
                    if (r != null && r.enabled != enabled)
                    {
                        r.enabled = enabled;
                    }
                }
            }
        }

        public static void UpdateSlotVisibility(PlayerBody.SlotView slotView, PlayerBody playerBody)
        {
            if (slotView == null || playerBody == null) return;

            var item = slotView._item;
            if (LogEquippedItems.Value && item != null && (CheckIsYourPlayer(playerBody) || CheckIsMenuPlayer(playerBody)))
            {
                try
                {
                    string id = item.StringTemplateId ?? item.TemplateId.ToString();
                    string shortName = item.Template?.ShortName ?? item.ShortName.Localized();
                    string fullName = item.Name.Localized();
                    Log?.LogInfo($"[HideArmor Item] Slot: {slotView.EquipmentSlot} | Name: \"{fullName}\" | ShortName: \"{shortName}\" | ID: \"{id}\"");
                }
                catch { }
            }

            bool shouldHide = ShouldHideSlot(slotView.EquipmentSlot, item, playerBody);
            SetSlotRenderersEnabled(slotView, !shouldHide);
        }

        public static void ApplyToPlayerBody(PlayerBody body)
        {
            if (body == null || body.SlotViews == null) return;

            int count = body.SlotViews.Count;
            for (int i = 0; i < count; i++)
            {
                var slotView = body.SlotViews.GetByIndex(i);
                if (slotView != null)
                {
                    UpdateSlotVisibility(slotView, body);
                }
            }
        }

        public static void ApplyToAllActiveBodies()
        {
            try
            {
                var bodies = FindObjectsOfType<PlayerBody>();
                if (bodies == null) return;

                for (int i = 0; i < bodies.Length; i++)
                {
                    ApplyToPlayerBody(bodies[i]);
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"Error in ApplyToAllActiveBodies: {ex}");
            }
        }
    }

    public static class SlotViewPatches
    {
        [HarmonyPatch(typeof(PlayerBody.SlotView), nameof(PlayerBody.SlotView.CreateAndParent))]
        [HarmonyPostfix]
        public static void CreateAndParentPostfix(PlayerBody.SlotView __instance, PlayerBody playerBody)
        {
            try
            {
                HideArmorPlugin.UpdateSlotVisibility(__instance, playerBody);
            }
            catch (Exception ex)
            {
                HideArmorPlugin.Log?.LogError($"Error in CreateAndParentPostfix: {ex}");
            }
        }
    }

    public static class PlayerBodyPatches
    {
        [HarmonyPatch(typeof(PlayerBody), nameof(PlayerBody.UpdatePlayerRenders))]
        [HarmonyPostfix]
        public static void UpdatePlayerRendersPostfix(PlayerBody __instance)
        {
            try
            {
                HideArmorPlugin.ApplyToPlayerBody(__instance);
            }
            catch (Exception ex)
            {
                HideArmorPlugin.Log?.LogError($"Error in UpdatePlayerRendersPostfix: {ex}");
            }
        }
    }
}
