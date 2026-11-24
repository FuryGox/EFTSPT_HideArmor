using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace HideArmor
{
    /// <summary>
    /// Configuration for a specific character (bot, boss, or other player).
    /// </summary>
    public class CharacterArmorConfig
    {
        public bool Enabled { get; set; } = true;
        public bool HideArmorVest { get; set; } = true;
        public bool HideArmoredChestRigs { get; set; } = true;
        public bool HideNonArmoredChestRigs { get; set; } = false;
        public bool HideArmoredHeadwear { get; set; } = false;
        public bool HideNonArmoredHeadwear { get; set; } = false;
        public bool HideBackpacks { get; set; } = false;
        public bool HideFaceCovers { get; set; } = false;
        public bool HideEyewear { get; set; } = false;
        public bool HideEarpieces { get; set; } = false;
        public bool HideArmBands { get; set; } = false;
        public bool HideHolsters { get; set; } = false;
    }

    /// <summary>
    /// Custom attributes recognized by BepInEx.ConfigurationManager via reflection.
    /// Setting Browsable = false hides the setting from the Configuration Manager UI.
    /// </summary>
    public class ConfigurationManagerAttributes
    {
        public bool? Browsable { get; set; }
        public bool? IsAdvanced { get; set; }
        public int? Order { get; set; }
        public bool? ReadOnly { get; set; }
        public string Category { get; set; }
    }

    [BepInPlugin("com.furyg.hidearmor", "Hide Armor", "1.2.1")]
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
        public static ConfigEntry<bool> LogUserInfo { get; private set; }

        // Section 2 - Equipment Slots (Current Player)
        public static ConfigEntry<bool> HideArmorVest { get; private set; }
        public static ConfigEntry<bool> HideArmoredChestRigs { get; private set; }
        public static ConfigEntry<bool> HideNonArmoredChestRigs { get; private set; }
        public static ConfigEntry<bool> HideArmoredHeadwear { get; private set; }
        public static ConfigEntry<bool> HideNonArmoredHeadwear { get; private set; }
        public static ConfigEntry<bool> HideBackpacks { get; private set; }
        public static ConfigEntry<bool> HideFaceCovers { get; private set; }
        public static ConfigEntry<bool> HideEyewear { get; private set; }
        public static ConfigEntry<bool> HideEarpieces { get; private set; }
        public static ConfigEntry<bool> HideArmBands { get; private set; }
        public static ConfigEntry<bool> HideHolsters { get; private set; }

        // Section 3 - Character Selection
        public static ConfigEntry<string> CharacterList { get; private set; }
        public static ConfigEntry<string> SelectedCharacter { get; private set; }

        // Section 4 - Selected Character Config
        public static ConfigEntry<bool> SelectedChar_Enabled { get; private set; }
        public static ConfigEntry<bool> SelectedChar_HideArmorVest { get; private set; }
        public static ConfigEntry<bool> SelectedChar_HideArmoredChestRigs { get; private set; }
        public static ConfigEntry<bool> SelectedChar_HideNonArmoredChestRigs { get; private set; }
        public static ConfigEntry<bool> SelectedChar_HideArmoredHeadwear { get; private set; }
        public static ConfigEntry<bool> SelectedChar_HideNonArmoredHeadwear { get; private set; }
        public static ConfigEntry<bool> SelectedChar_HideBackpacks { get; private set; }
        public static ConfigEntry<bool> SelectedChar_HideFaceCovers { get; private set; }
        public static ConfigEntry<bool> SelectedChar_HideEyewear { get; private set; }
        public static ConfigEntry<bool> SelectedChar_HideEarpieces { get; private set; }
        public static ConfigEntry<bool> SelectedChar_HideArmBands { get; private set; }
        public static ConfigEntry<bool> SelectedChar_HideHolsters { get; private set; }

        private static readonly FieldInfo IsYourPlayerField = typeof(PlayerBody).GetField("_isYourPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo PlayerProfileIdField = typeof(PlayerBody).GetField("_playerProfileID", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo AcceptableValuesField = typeof(ConfigDescription).GetField("<AcceptableValues>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Dictionary<string, string[]> BossAliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Killa", new[] { "Килла" } },
            { "Tagilla", new[] { "Тагилла" } },
            { "Reshala", new[] { "Решала" } },
            { "Glukhar", new[] { "Глухарь" } },
            { "Sanitar", new[] { "Санитар" } },
            { "Shturman", new[] { "Штурман" } },
            { "Kaban", new[] { "Кабан" } },
            { "Kolontay", new[] { "Колонтай" } },
            { "Zryachiy", new[] { "Зрячий" } }
        };

        private static readonly Dictionary<string, CharacterArmorConfig> CharacterProfiles = new Dictionary<string, CharacterArmorConfig>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> LoggedIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static string ProfilesFilePath => Path.Combine(BepInEx.Paths.ConfigPath, "HideArmor_Characters.json");

        private static readonly List<ConfigurationManagerAttributes> CharacterConfigAttributes = new List<ConfigurationManagerAttributes>();

        private static bool _isLoadingCharacterConfig = false;
        private static string _lastSelectedCharacter = "";
        private float _nextCheckTime = 0f;

        private static ConfigurationManagerAttributes CreateManagedAttribute(bool visible)
        {
            var attr = new ConfigurationManagerAttributes { Browsable = visible };
            CharacterConfigAttributes.Add(attr);
            return attr;
        }

        private static void UpdateCharacterConfigVisibility(bool visible)
        {
            for (int i = 0; i < CharacterConfigAttributes.Count; i++)
            {
                CharacterConfigAttributes[i].Browsable = visible;
            }
            RefreshConfigurationManager();
        }

        private static void RefreshConfigurationManager()
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name == "ConfigurationManager")
                    {
                        var cmType = asm.GetType("ConfigurationManager.ConfigurationManager");
                        if (cmType != null)
                        {
                            var instanceProp = cmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                            var instance = instanceProp?.GetValue(null);
                            if (instance != null)
                            {
                                var buildMethod = cmType.GetMethod("BuildSettingList", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                buildMethod?.Invoke(instance, null);
                            }
                        }
                        break;
                    }
                }
            }
            catch { }
        }

        private void Awake()
        {
            Instance = this;

            LoadProfilesFromFile();
            InitConfig();

            // Load initial selected character's configuration into Section 4 if characters exist
            string[] names = ParseCharacterNames(CharacterList?.Value);
            if (names.Length > 0 && !string.IsNullOrWhiteSpace(SelectedCharacter?.Value))
            {
                _lastSelectedCharacter = SelectedCharacter.Value.Trim();
                LoadProfileToUIOptions(_lastSelectedCharacter);
            }

            try
            {
                var harmony = new Harmony("com.furyg.hidearmor");
                harmony.PatchAll(typeof(SlotViewPatches));
                harmony.PatchAll(typeof(PlayerBodyPatches));
                harmony.PatchAll(typeof(PlayerPatches));
                harmony.PatchAll(typeof(PlayerModelViewPatches));
                harmony.PatchAll(typeof(GameWorldPatches));
                Logger.LogInfo("Hide Armor mod loaded and all patches registered successfully!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to patch EFT methods: {ex}");
            }
        }

        private void InitConfig()
        {
            // Section 1: General Settings
            const string secGeneral = "1. General Settings";
            ModEnabled = Config.Bind(secGeneral, "Enabled", true, "Master toggle to enable or disable the Hide Armor mod.");
            ToggleHotkey = Config.Bind(secGeneral, "Toggle Hotkey", new KeyboardShortcut(KeyCode.None), "Optional hotkey to toggle armor visibility on the fly in-raid.");
            HideForPlayer = Config.Bind(secGeneral, "Hide For Local Player", true, "Apply armor hiding to your character in-raid (uses Section 2 Equipment Slots).");
            HideInMenu = Config.Bind(secGeneral, "Hide In Menu / Inventory", true, "Apply armor hiding to your character model in main menu / stash inspect (uses Section 2 Equipment Slots).");
            HideForBots = Config.Bind(secGeneral, "Hide For Other Bots (Not In List)", false, "Hide armor on other AI bots not specified in the Character List (default: false).");
            LogUserInfo = Config.Bind(secGeneral, "Log Character Info", true, "When any character/bot spawns, print its exact Nickname, Profile ID, and Account ID to the BepInEx console.");

            // Section 2: Equipment Slots (Current Player)
            const string secPlayerSlots = "2. Equipment Slots (Current Player)";
            HideArmorVest = Config.Bind(secPlayerSlots, "Hide Armor Vest", true, "Hide body armor in the ArmorVest slot (e.g. Slick, Korund, PACA, Trooper).");
            HideArmoredChestRigs = Config.Bind(secPlayerSlots, "Hide Armored Chest Rigs", true, "Hide chest rigs in the TacticalVest slot ONLY if they provide armor (e.g. AVS, TV-110, CPC, Thor CR).");
            HideNonArmoredChestRigs = Config.Bind(secPlayerSlots, "Hide Non-Armored Chest Rigs", false, "Hide chest rigs in the TacticalVest slot even if unarmored (e.g. Bank Robber, Triton, Blackrock).");
            HideArmoredHeadwear = Config.Bind(secPlayerSlots, "Hide Armored Headwear (Helmets)", false, "Hide helmets and armored headgear in the Headwear slot (e.g. Altyn, Fast MT, Team Wendy).");
            HideNonArmoredHeadwear = Config.Bind(secPlayerSlots, "Hide Non-Armored Headwear (Hats/Caps)", false, "Hide non-armored headwear in the Headwear slot (e.g. baseball caps, beanies, berets).");
            HideBackpacks = Config.Bind(secPlayerSlots, "Hide Backpacks", false, "Hide backpacks in the Backpack slot.");
            HideFaceCovers = Config.Bind(secPlayerSlots, "Hide Face Covers", false, "Hide balaclavas, masks, and shemaghs in the FaceCover slot.");
            HideEyewear = Config.Bind(secPlayerSlots, "Hide Eyewear", false, "Hide glasses and goggles in the Eyewear slot.");
            HideEarpieces = Config.Bind(secPlayerSlots, "Hide Earpieces (Headsets)", false, "Hide headsets and earpieces in the Earpiece slot (e.g. ComTac 2, Sordin, Razor).");
            HideArmBands = Config.Bind(secPlayerSlots, "Hide Armbands", false, "Hide armbands in the ArmBand slot.");
            HideHolsters = Config.Bind(secPlayerSlots, "Hide Holsters", false, "Hide sidearm holsters in the Holster slot.");

            // Section 3: Character Selection
            const string secCharacterSelect = "3. Character Selection";
            string defaultList = null;
            CharacterList = Config.Bind(secCharacterSelect, "Character List", defaultList,
                "Comma-separated list of character names, bot names, or IDs to configure. Add or remove names here. Leave empty to disable character selection.");

            string[] names = ParseCharacterNames(CharacterList.Value);
            bool hasCharacters = names.Length > 0;
            string defaultSelected = hasCharacters ? names[0] : "";

            SelectedCharacter = Config.Bind(secCharacterSelect, "Selected Character", defaultSelected,
                new ConfigDescription("Select a character from the list above to view and edit their options in Section 4 below.",
                hasCharacters ? new AcceptableValueList<string>(names) : null,
                CreateManagedAttribute(hasCharacters)));

            // Section 4: Selected Character Config
            const string secSelectedChar = "4. Selected Character Config";
            SelectedChar_Enabled = Config.Bind(secSelectedChar, "Custom Hiding Enabled", true,
                new ConfigDescription("Enable custom armor hiding for the currently selected character.", null, CreateManagedAttribute(hasCharacters)));
            SelectedChar_HideArmorVest = Config.Bind(secSelectedChar, "Hide Armor Vest", true,
                new ConfigDescription("Hide body armor vest for the selected character.", null, CreateManagedAttribute(hasCharacters)));
            SelectedChar_HideArmoredChestRigs = Config.Bind(secSelectedChar, "Hide Armored Chest Rigs", true,
                new ConfigDescription("Hide armored chest rigs (TacticalVest) for the selected character.", null, CreateManagedAttribute(hasCharacters)));
            SelectedChar_HideNonArmoredChestRigs = Config.Bind(secSelectedChar, "Hide Non-Armored Chest Rigs", false,
                new ConfigDescription("Hide unarmored chest rigs (TacticalVest) for the selected character.", null, CreateManagedAttribute(hasCharacters)));
            SelectedChar_HideArmoredHeadwear = Config.Bind(secSelectedChar, "Hide Armored Headwear (Helmets)", false,
                new ConfigDescription("Hide helmets/armored headwear (Headwear) for the selected character.", null, CreateManagedAttribute(hasCharacters)));
            SelectedChar_HideNonArmoredHeadwear = Config.Bind(secSelectedChar, "Hide Non-Armored Headwear", false,
                new ConfigDescription("Hide non-armored hats/caps (Headwear) for the selected character.", null, CreateManagedAttribute(hasCharacters)));
            SelectedChar_HideBackpacks = Config.Bind(secSelectedChar, "Hide Backpacks", false,
                new ConfigDescription("Hide backpacks for the selected character.", null, CreateManagedAttribute(hasCharacters)));
            SelectedChar_HideFaceCovers = Config.Bind(secSelectedChar, "Hide Face Covers", false,
                new ConfigDescription("Hide face covers/masks for the selected character.", null, CreateManagedAttribute(hasCharacters)));
            SelectedChar_HideEyewear = Config.Bind(secSelectedChar, "Hide Eyewear", false,
                new ConfigDescription("Hide eyewear/glasses for the selected character.", null, CreateManagedAttribute(hasCharacters)));
            SelectedChar_HideEarpieces = Config.Bind(secSelectedChar, "Hide Earpieces (Headsets)", false,
                new ConfigDescription("Hide headsets for the selected character.", null, CreateManagedAttribute(hasCharacters)));
            SelectedChar_HideArmBands = Config.Bind(secSelectedChar, "Hide Armbands", false,
                new ConfigDescription("Hide armbands for the selected character.", null, CreateManagedAttribute(hasCharacters)));
            SelectedChar_HideHolsters = Config.Bind(secSelectedChar, "Hide Holsters", false,
                new ConfigDescription("Hide holsters for the selected character.", null, CreateManagedAttribute(hasCharacters)));

            // Listeners for Sections 1 & 2
            ModEnabled.SettingChanged += OnGeneralConfigChanged;
            HideForPlayer.SettingChanged += OnGeneralConfigChanged;
            HideInMenu.SettingChanged += OnGeneralConfigChanged;
            HideForBots.SettingChanged += OnGeneralConfigChanged;
            HideArmorVest.SettingChanged += OnGeneralConfigChanged;
            HideArmoredChestRigs.SettingChanged += OnGeneralConfigChanged;
            HideNonArmoredChestRigs.SettingChanged += OnGeneralConfigChanged;
            HideArmoredHeadwear.SettingChanged += OnGeneralConfigChanged;
            HideNonArmoredHeadwear.SettingChanged += OnGeneralConfigChanged;
            HideBackpacks.SettingChanged += OnGeneralConfigChanged;
            HideFaceCovers.SettingChanged += OnGeneralConfigChanged;
            HideEyewear.SettingChanged += OnGeneralConfigChanged;
            HideEarpieces.SettingChanged += OnGeneralConfigChanged;
            HideArmBands.SettingChanged += OnGeneralConfigChanged;
            HideHolsters.SettingChanged += OnGeneralConfigChanged;

            // Listeners for Section 3
            CharacterList.SettingChanged += OnCharacterListChanged;
            SelectedCharacter.SettingChanged += OnSelectedCharacterChanged;

            // Listeners for Section 4
            SelectedChar_Enabled.SettingChanged += OnSelectedCharOptionChanged;
            SelectedChar_HideArmorVest.SettingChanged += OnSelectedCharOptionChanged;
            SelectedChar_HideArmoredChestRigs.SettingChanged += OnSelectedCharOptionChanged;
            SelectedChar_HideNonArmoredChestRigs.SettingChanged += OnSelectedCharOptionChanged;
            SelectedChar_HideArmoredHeadwear.SettingChanged += OnSelectedCharOptionChanged;
            SelectedChar_HideNonArmoredHeadwear.SettingChanged += OnSelectedCharOptionChanged;
            SelectedChar_HideBackpacks.SettingChanged += OnSelectedCharOptionChanged;
            SelectedChar_HideFaceCovers.SettingChanged += OnSelectedCharOptionChanged;
            SelectedChar_HideEyewear.SettingChanged += OnSelectedCharOptionChanged;
            SelectedChar_HideEarpieces.SettingChanged += OnSelectedCharOptionChanged;
            SelectedChar_HideArmBands.SettingChanged += OnSelectedCharOptionChanged;
            SelectedChar_HideHolsters.SettingChanged += OnSelectedCharOptionChanged;
        }

        private static string[] ParseCharacterNames(string listString)
        {
            if (string.IsNullOrWhiteSpace(listString))
            {
                return Array.Empty<string>();
            }

            var parts = listString.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string clean = parts[i].Trim();
                if (!string.IsNullOrEmpty(clean) && !result.Contains(clean))
                {
                    result.Add(clean);
                }
            }

            return result.ToArray();
        }

        private void OnGeneralConfigChanged(object sender, EventArgs e)
        {
            ApplyToAllActiveBodies();
        }

        private void OnCharacterListChanged(object sender, EventArgs e)
        {
            if (_isLoadingCharacterConfig) return;

            string[] names = ParseCharacterNames(CharacterList?.Value);
            bool hasCharacters = names.Length > 0;
            UpdateCharacterConfigVisibility(hasCharacters);

            if (!hasCharacters)
            {
                UpdateSelectedCharacterDropdown(names);
                ApplyToAllActiveBodies();
                return;
            }

            var validSet = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

            foreach (var name in names)
            {
                if (!CharacterProfiles.ContainsKey(name))
                {
                    CharacterProfiles[name] = new CharacterArmorConfig();
                }
            }

            // Remove garbage/stale typing fragment keys that are not in valid names
            var toRemove = new List<string>();
            foreach (var key in CharacterProfiles.Keys)
            {
                if (!validSet.Contains(key) && (key.Length < 3 || key.Length <= 2))
                {
                    toRemove.Add(key);
                }
            }
            foreach (var key in toRemove)
            {
                CharacterProfiles.Remove(key);
            }

            UpdateSelectedCharacterDropdown(names);
            SaveProfilesToFile();
            ApplyToAllActiveBodies();
        }

        private static void UpdateSelectedCharacterDropdown(string[] names)
        {
            if (SelectedCharacter == null) return;

            if (names == null || names.Length == 0)
            {
                if (AcceptableValuesField != null)
                {
                    try
                    {
                        AcceptableValuesField.SetValue(SelectedCharacter.Description, null);
                    }
                    catch { }
                }
                SelectedCharacter.Value = "";
                return;
            }

            if (AcceptableValuesField != null)
            {
                try
                {
                    AcceptableValuesField.SetValue(SelectedCharacter.Description, new AcceptableValueList<string>(names));
                }
                catch (Exception ex)
                {
                    Log?.LogWarning($"Failed to update AcceptableValues on SelectedCharacter: {ex.Message}");
                }
            }

            bool contains = false;
            foreach (var n in names)
            {
                if (n.Equals(SelectedCharacter.Value, StringComparison.OrdinalIgnoreCase))
                {
                    contains = true;
                    break;
                }
            }

            if (!contains && names.Length > 0)
            {
                SelectedCharacter.Value = names[0];
            }
        }

        private void OnSelectedCharacterChanged(object sender, EventArgs e)
        {
            if (_isLoadingCharacterConfig) return;

            string newChar = SelectedCharacter.Value?.Trim();
            if (string.IsNullOrEmpty(newChar)) return;

            if (!string.IsNullOrEmpty(_lastSelectedCharacter))
            {
                SaveCurrentUIOptionsToProfile(_lastSelectedCharacter);
            }

            _lastSelectedCharacter = newChar;

            LoadProfileToUIOptions(newChar);
            SaveProfilesToFile();
            ApplyToAllActiveBodies();
        }

        private void OnSelectedCharOptionChanged(object sender, EventArgs e)
        {
            if (_isLoadingCharacterConfig) return;

            string currentChar = SelectedCharacter.Value?.Trim();
            if (string.IsNullOrEmpty(currentChar)) return;

            SaveCurrentUIOptionsToProfile(currentChar);
            SaveProfilesToFile();
            ApplyToAllActiveBodies();
        }

        private static void LoadProfileToUIOptions(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName)) return;

            if (!CharacterProfiles.TryGetValue(characterName, out var config))
            {
                config = new CharacterArmorConfig();
                CharacterProfiles[characterName] = config;
            }

            _isLoadingCharacterConfig = true;
            try
            {
                SelectedChar_Enabled.Value = config.Enabled;
                SelectedChar_HideArmorVest.Value = config.HideArmorVest;
                SelectedChar_HideArmoredChestRigs.Value = config.HideArmoredChestRigs;
                SelectedChar_HideNonArmoredChestRigs.Value = config.HideNonArmoredChestRigs;
                SelectedChar_HideArmoredHeadwear.Value = config.HideArmoredHeadwear;
                SelectedChar_HideNonArmoredHeadwear.Value = config.HideNonArmoredHeadwear;
                SelectedChar_HideBackpacks.Value = config.HideBackpacks;
                SelectedChar_HideFaceCovers.Value = config.HideFaceCovers;
                SelectedChar_HideEyewear.Value = config.HideEyewear;
                SelectedChar_HideEarpieces.Value = config.HideEarpieces;
                SelectedChar_HideArmBands.Value = config.HideArmBands;
                SelectedChar_HideHolsters.Value = config.HideHolsters;
            }
            finally
            {
                _isLoadingCharacterConfig = false;
            }
        }

        private static void SaveCurrentUIOptionsToProfile(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName)) return;

            if (!CharacterProfiles.TryGetValue(characterName, out var config))
            {
                config = new CharacterArmorConfig();
                CharacterProfiles[characterName] = config;
            }

            config.Enabled = SelectedChar_Enabled.Value;
            config.HideArmorVest = SelectedChar_HideArmorVest.Value;
            config.HideArmoredChestRigs = SelectedChar_HideArmoredChestRigs.Value;
            config.HideNonArmoredChestRigs = SelectedChar_HideNonArmoredChestRigs.Value;
            config.HideArmoredHeadwear = SelectedChar_HideArmoredHeadwear.Value;
            config.HideNonArmoredHeadwear = SelectedChar_HideNonArmoredHeadwear.Value;
            config.HideBackpacks = SelectedChar_HideBackpacks.Value;
            config.HideFaceCovers = SelectedChar_HideFaceCovers.Value;
            config.HideEyewear = SelectedChar_HideEyewear.Value;
            config.HideEarpieces = SelectedChar_HideEarpieces.Value;
            config.HideArmBands = SelectedChar_HideArmBands.Value;
            config.HideHolsters = SelectedChar_HideHolsters.Value;
        }

        private static void LoadProfilesFromFile()
        {
            try
            {
                if (File.Exists(ProfilesFilePath))
                {
                    string json = File.ReadAllText(ProfilesFilePath);
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, CharacterArmorConfig>>(json);
                    if (dict != null)
                    {
                        CharacterProfiles.Clear();
                        foreach (var kvp in dict)
                        {
                            CharacterProfiles[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"Failed to load character profiles: {ex}");
            }
        }

        private static void SaveProfilesToFile()
        {
            try
            {
                string json = JsonConvert.SerializeObject(CharacterProfiles, Formatting.Indented);
                File.WriteAllText(ProfilesFilePath, json);
            }
            catch (Exception ex)
            {
                Log?.LogError($"Failed to save character profiles: {ex}");
            }
        }

        public static bool IsExactMatch(string pattern, string nickname, string profileId, string accountId)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return false;
            pattern = pattern.Trim();

            if (!string.IsNullOrEmpty(nickname) && nickname.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(profileId) && profileId.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(accountId) && accountId.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        public static bool IsBossAliasMatch(string pattern, string nickname)
        {
            if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(nickname)) return false;
            pattern = pattern.Trim();

            if (BossAliases.TryGetValue(pattern, out var aliases))
            {
                for (int i = 0; i < aliases.Length; i++)
                {
                    if (nickname.Equals(aliases[i], StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            foreach (var kvp in BossAliases)
            {
                if (kvp.Key.Equals(nickname, StringComparison.OrdinalIgnoreCase))
                {
                    for (int i = 0; i < kvp.Value.Length; i++)
                    {
                        if (pattern.Equals(kvp.Value[i], StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }

            return false;
        }

        public static bool IsPartialMatch(string pattern, string nickname, string profileId, string accountId)
        {
            if (string.IsNullOrWhiteSpace(pattern) || pattern.Trim().Length < 3) return false;
            pattern = pattern.Trim();

            if (!string.IsNullOrEmpty(nickname) && nickname.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (!string.IsNullOrEmpty(profileId) && profileId.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (!string.IsNullOrEmpty(accountId) && accountId.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        public static CharacterArmorConfig FindCharacterConfig(string nickname, string profileId, string accountId)
        {
            string[] validNames = ParseCharacterNames(CharacterList?.Value);
            if (validNames == null || validNames.Length == 0) return null;

            // Pass 1: Exact match against valid names in CharacterList
            for (int i = 0; i < validNames.Length; i++)
            {
                string name = validNames[i];
                if (IsExactMatch(name, nickname, profileId, accountId))
                {
                    if (CharacterProfiles.TryGetValue(name, out var config))
                    {
                        return config;
                    }
                }
            }

            // Pass 2: Boss Cyrillic/English alias match (e.g. Tagilla <-> Тагилла)
            for (int i = 0; i < validNames.Length; i++)
            {
                string name = validNames[i];
                if (IsBossAliasMatch(name, nickname))
                {
                    if (CharacterProfiles.TryGetValue(name, out var config))
                    {
                        return config;
                    }
                }
            }

            // Pass 3: Substring match (ONLY allowed for patterns of 3+ chars, sorted longest first)
            var candidates = new List<string>();
            for (int i = 0; i < validNames.Length; i++)
            {
                if (validNames[i].Length >= 3)
                {
                    candidates.Add(validNames[i]);
                }
            }
            candidates.Sort((a, b) => b.Length.CompareTo(a.Length));

            for (int i = 0; i < candidates.Count; i++)
            {
                string name = candidates[i];
                if (IsPartialMatch(name, nickname, profileId, accountId))
                {
                    if (CharacterProfiles.TryGetValue(name, out var config))
                    {
                        return config;
                    }
                }
            }

            return null;
        }

        private void Update()
        {
            if (ToggleHotkey != null && ToggleHotkey.Value.IsDown())
            {
                ModEnabled.Value = !ModEnabled.Value;
                Log?.LogInfo($"Hide Armor toggled: {(ModEnabled.Value ? "ENABLED" : "DISABLED")}");
                ApplyToAllActiveBodies();
            }

            // Periodic safety check to ensure local player's armor is kept hidden automatically
            if (ModEnabled.Value && Time.time >= _nextCheckTime)
            {
                _nextCheckTime = Time.time + 1.0f;
                if (Comfort.Common.Singleton<GameWorld>.Instantiated)
                {
                    var mainPlayer = Comfort.Common.Singleton<GameWorld>.Instance?.MainPlayer;
                    if (mainPlayer != null && mainPlayer.PlayerBody != null)
                    {
                        ApplyToPlayerBody(mainPlayer.PlayerBody);
                    }
                }
            }
        }

        /// <summary>
        /// Reliably checks if the given PlayerBody belongs to the current human player (in raid or menu).
        /// </summary>
        public static bool CheckIsCurrentPlayer(PlayerBody playerBody)
        {
            if (playerBody == null) return false;

            // 1. Menu / Stash preview character model
            if (playerBody.GetComponentInParent<MenuPlayerPoser>() != null || playerBody.GetComponent<MenuPlayerPoser>() != null)
                return true;

            if (playerBody.GetComponentInParent<EFT.UI.PlayerModelView>() != null || playerBody.GetComponent<EFT.UI.PlayerModelView>() != null)
                return true;

            // 2. In-raid GameWorld local MainPlayer
            try
            {
                if (Comfort.Common.Singleton<GameWorld>.Instantiated)
                {
                    var mainPlayer = Comfort.Common.Singleton<GameWorld>.Instance?.MainPlayer;
                    if (mainPlayer != null)
                    {
                        if (mainPlayer.PlayerBody == playerBody)
                            return true;

                        if (mainPlayer.gameObject == playerBody.gameObject)
                            return true;

                        var pbInMain = mainPlayer.GetComponentInChildren<PlayerBody>();
                        if (pbInMain == playerBody)
                            return true;
                    }
                }
            }
            catch { }

            // 3. Player component on self or parent
            var player = playerBody.GetComponentInParent<Player>() ?? playerBody.GetComponent<Player>();
            if (player != null && player.IsYourPlayer)
                return true;

            // 4. Private _isYourPlayer field on PlayerBody
            if (IsYourPlayerField != null)
            {
                try
                {
                    if ((bool)IsYourPlayerField.GetValue(playerBody))
                        return true;
                }
                catch { }
            }

            // 5. Match against client backend session Profile ID / Account ID (handles early raid loading before MainPlayer is registered)
            try
            {
                if (Comfort.Common.Singleton<TarkovApplication>.Instantiated)
                {
                    var app = Comfort.Common.Singleton<TarkovApplication>.Instance;
                    var session = app?.GetClientBackEndSession();
                    if (session?.Profile != null)
                    {
                        var (nick, pId, aId) = GetPlayerIdentity(playerBody);
                        if (!string.IsNullOrEmpty(pId))
                        {
                            if (pId.Equals(session.Profile.Id, StringComparison.OrdinalIgnoreCase) ||
                                pId.Equals(session.Profile.AccountId, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                        if (!string.IsNullOrEmpty(aId) && aId.Equals(session.Profile.AccountId, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        public static (string nickname, string profileId, string accountId) GetPlayerIdentity(PlayerBody playerBody)
        {
            if (playerBody == null) return ("", "", "");

            // 1. Player component
            var player = playerBody.GetComponentInParent<Player>() ?? playerBody.GetComponent<Player>();
            if (player != null)
            {
                string nick = player.Profile?.Nickname ?? "";
                string profId = player.ProfileId ?? player.Profile?.Id ?? "";
                string accId = player.AccountId ?? player.Profile?.AccountId ?? "";
                return (nick, profId, accId);
            }

            // 2. ObservedPlayerView component (bots and other players)
            var observed = playerBody.GetComponentInParent<EFT.NextObservedPlayer.ObservedPlayerView>();
            if (observed != null)
            {
                string nick = observed.NickName ?? observed.Profile?.Nickname ?? "";
                string profId = observed.ProfileId ?? observed.Profile?.Id ?? "";
                string accId = observed.AccountId ?? observed.Profile?.AccountId ?? "";
                return (nick, profId, accId);
            }

            // 3. PlayerBody _playerProfileID field
            string bodyProfileId = "";
            if (PlayerProfileIdField != null)
            {
                try
                {
                    bodyProfileId = PlayerProfileIdField.GetValue(playerBody) as string ?? "";
                }
                catch { }
            }

            // 4. Menu preview: check backend session profile
            if (playerBody.GetComponentInParent<MenuPlayerPoser>() != null || playerBody.GetComponent<MenuPlayerPoser>() != null)
            {
                try
                {
                    if (Comfort.Common.Singleton<TarkovApplication>.Instantiated)
                    {
                        var app = Comfort.Common.Singleton<TarkovApplication>.Instance;
                        if (app != null)
                        {
                            var session = app.GetClientBackEndSession();
                            if (session != null && session.Profile != null)
                            {
                                string nick = session.Profile.Nickname ?? "";
                                string pId = session.Profile.Id ?? bodyProfileId;
                                string aId = session.Profile.AccountId ?? "";
                                return (nick, pId, aId);
                            }
                        }
                    }
                }
                catch { }
            }

            return ("", bodyProfileId, "");
        }

        public static bool MatchesUser(string pattern, string nickname, string profileId, string accountId)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return false;

            pattern = pattern.Trim();
            if (pattern.Length == 0) return false;

            if (!string.IsNullOrEmpty(nickname) && nickname.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (!string.IsNullOrEmpty(profileId) && profileId.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (!string.IsNullOrEmpty(accountId) && accountId.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
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

        private static bool CheckSlotRule(EquipmentSlot slot, Item item,
            bool hideArmorVest,
            bool hideArmoredChestRigs,
            bool hideNonArmoredChestRigs,
            bool hideArmoredHeadwear,
            bool hideNonArmoredHeadwear,
            bool hideBackpacks,
            bool hideFaceCovers,
            bool hideEyewear,
            bool hideArmBands,
            bool hideHolsters,
            bool hideEarpieces)
        {
            switch (slot)
            {
                case EquipmentSlot.ArmorVest:
                    return hideArmorVest;

                case EquipmentSlot.TacticalVest:
                    if (item != null)
                    {
                        return IsArmoredItem(item) ? hideArmoredChestRigs : hideNonArmoredChestRigs;
                    }
                    return hideArmoredChestRigs || hideNonArmoredChestRigs;

                case EquipmentSlot.Headwear:
                    if (item != null)
                    {
                        return IsArmoredItem(item) ? hideArmoredHeadwear : hideNonArmoredHeadwear;
                    }
                    return hideArmoredHeadwear || hideNonArmoredHeadwear;

                case EquipmentSlot.Backpack:
                    return hideBackpacks;

                case EquipmentSlot.FaceCover:
                    return hideFaceCovers;

                case EquipmentSlot.Eyewear:
                    return hideEyewear;

                case EquipmentSlot.ArmBand:
                    return hideArmBands;

                case EquipmentSlot.Holster:
                    return hideHolsters;

                case EquipmentSlot.Earpiece:
                    return hideEarpieces;

                default:
                    return false;
            }
        }

        public static bool ShouldHideSlot(EquipmentSlot slot, Item item, PlayerBody playerBody)
        {
            if (!ModEnabled.Value) return false;
            if (playerBody == null) return false;

            bool isCurrentPlayer = CheckIsCurrentPlayer(playerBody);
            bool isMenu = playerBody.GetComponentInParent<MenuPlayerPoser>() != null || playerBody.GetComponent<MenuPlayerPoser>() != null;

            // Check if this character matches any configured profile in Character List
            var (nickname, profileId, accountId) = GetPlayerIdentity(playerBody);
            var customConfig = FindCharacterConfig(nickname, profileId, accountId);

            // 1. Current player (local player in raid or menu)
            if (isCurrentPlayer)
            {
                if (isMenu)
                {
                    if (!HideInMenu.Value) return false;
                }
                else
                {
                    if (!HideForPlayer.Value) return false;
                }

                // If local player was explicitly added to Character List and has a custom profile, use it!
                if (customConfig != null)
                {
                    if (!customConfig.Enabled) return false;

                    return CheckSlotRule(slot, item,
                        customConfig.HideArmorVest,
                        customConfig.HideArmoredChestRigs,
                        customConfig.HideNonArmoredChestRigs,
                        customConfig.HideArmoredHeadwear,
                        customConfig.HideNonArmoredHeadwear,
                        customConfig.HideBackpacks,
                        customConfig.HideFaceCovers,
                        customConfig.HideEyewear,
                        customConfig.HideArmBands,
                        customConfig.HideHolsters,
                        customConfig.HideEarpieces);
                }

                // Default for Current Player: use Section 2 "Equipment Slots (Current Player)"
                return CheckSlotRule(slot, item,
                    HideArmorVest.Value,
                    HideArmoredChestRigs.Value,
                    HideNonArmoredChestRigs.Value,
                    HideArmoredHeadwear.Value,
                    HideNonArmoredHeadwear.Value,
                    HideBackpacks.Value,
                    HideFaceCovers.Value,
                    HideEyewear.Value,
                    HideArmBands.Value,
                    HideHolsters.Value,
                    HideEarpieces.Value);
            }

            // 2. Other characters (Bots, bosses, co-op players)
            if (customConfig != null)
            {
                if (!customConfig.Enabled) return false;

                return CheckSlotRule(slot, item,
                    customConfig.HideArmorVest,
                    customConfig.HideArmoredChestRigs,
                    customConfig.HideNonArmoredChestRigs,
                    customConfig.HideArmoredHeadwear,
                    customConfig.HideNonArmoredHeadwear,
                    customConfig.HideBackpacks,
                    customConfig.HideFaceCovers,
                    customConfig.HideEyewear,
                    customConfig.HideArmBands,
                    customConfig.HideHolsters,
                    customConfig.HideEarpieces);
            }

            // 3. Fallback for bots not in the character list
            if (!HideForBots.Value) return false;

            return CheckSlotRule(slot, item,
                HideArmorVest.Value,
                HideArmoredChestRigs.Value,
                HideNonArmoredChestRigs.Value,
                HideArmoredHeadwear.Value,
                HideNonArmoredHeadwear.Value,
                HideBackpacks.Value,
                HideFaceCovers.Value,
                HideEyewear.Value,
                HideArmBands.Value,
                HideHolsters.Value,
                HideEarpieces.Value);
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

            // 2. Skinned dresses (armor / clothes)
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

            // 3. Child renderers in Model hierarchy (plates, attachments, pouches)
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

            // Resolve item from all potential sources
            Item item = slotView._item;
            if (item == null && slotView._slot != null)
            {
                item = slotView._slot.ContainedItem;
            }
            if (item == null && slotView.ContainedItem != null)
            {
                item = slotView.ContainedItem.Value;
            }
            if (item == null && playerBody.Equipment != null)
            {
                item = playerBody.Equipment.GetSlot(slotView.EquipmentSlot)?.ContainedItem;
            }

            bool shouldHide = ShouldHideSlot(slotView.EquipmentSlot, item, playerBody);
            SetSlotRenderersEnabled(slotView, !shouldHide);
        }

        public static void ApplyToPlayerBody(PlayerBody body)
        {
            if (body == null || body.SlotViews == null) return;

            if (LogUserInfo.Value)
            {
                try
                {
                    var (nick, pId, aId) = GetPlayerIdentity(body);
                    string key = $"{nick}_{pId}_{aId}";
                    if (!LoggedIdentities.Contains(key) && (!string.IsNullOrEmpty(nick) || !string.IsNullOrEmpty(pId)))
                    {
                        LoggedIdentities.Add(key);
                        bool isCurrent = CheckIsCurrentPlayer(body);
                        var matched = FindCharacterConfig(nick, pId, aId);
                        string matchInfo = matched != null ? " [Matched Custom Profile]" : "";
                        Log?.LogInfo($"[HideArmor Character] Nickname: \"{nick}\" | ProfileID: \"{pId}\" | AccountID: \"{aId}\" | IsCurrentPlayer: {isCurrent}{matchInfo}");
                    }
                }
                catch { }
            }

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
                PlayerBody targetBody = playerBody ?? __instance._playerBody;
                HideArmorPlugin.UpdateSlotVisibility(__instance, targetBody);
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

    public static class PlayerPatches
    {
        [HarmonyPatch(typeof(Player), nameof(Player.InitVisuals))]
        [HarmonyPostfix]
        public static void InitVisualsPostfix(Player __instance)
        {
            try
            {
                if (__instance != null && __instance.PlayerBody != null)
                {
                    HideArmorPlugin.ApplyToPlayerBody(__instance.PlayerBody);
                }
            }
            catch (Exception ex)
            {
                HideArmorPlugin.Log?.LogError($"Error in InitVisualsPostfix: {ex}");
            }
        }

        [HarmonyPatch(typeof(Player), "set_PointOfView")]
        [HarmonyPostfix]
        public static void SetPointOfViewPostfix(Player __instance)
        {
            try
            {
                if (__instance != null && __instance.PlayerBody != null)
                {
                    HideArmorPlugin.ApplyToPlayerBody(__instance.PlayerBody);
                }
            }
            catch (Exception ex)
            {
                HideArmorPlugin.Log?.LogError($"Error in SetPointOfViewPostfix: {ex}");
            }
        }
    }

    public static class PlayerModelViewPatches
    {
        [HarmonyPatch(typeof(EFT.UI.PlayerModelView), nameof(EFT.UI.PlayerModelView.OnLoadingCompleted))]
        [HarmonyPostfix]
        public static void OnLoadingCompletedPostfix(EFT.UI.PlayerModelView __instance)
        {
            try
            {
                if (__instance != null && __instance.PlayerBody != null)
                {
                    HideArmorPlugin.ApplyToPlayerBody(__instance.PlayerBody);
                }
            }
            catch (Exception ex)
            {
                HideArmorPlugin.Log?.LogError($"Error in PlayerModelView.OnLoadingCompletedPostfix: {ex}");
            }
        }
    }

    public static class GameWorldPatches
    {
        [HarmonyPatch(typeof(GameWorld), nameof(GameWorld.RegisterPlayer))]
        [HarmonyPostfix]
        public static void RegisterPlayerPostfix(IPlayer iPlayer)
        {
            try
            {
                if (iPlayer is Player player && player.PlayerBody != null)
                {
                    HideArmorPlugin.ApplyToPlayerBody(player.PlayerBody);
                }
            }
            catch (Exception ex)
            {
                HideArmorPlugin.Log?.LogError($"Error in GameWorld.RegisterPlayerPostfix: {ex}");
            }
        }
    }
}
