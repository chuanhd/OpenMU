// <copyright file="ClaimStarterPackageChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which lets a player claim a configurable starter package once per account.
/// </summary>
[Guid("CB943088-2238-44B2-8D5E-D595AF88D06E")]
[PlugIn]
[Display(Name = "Claim Starter Package", Description = "Lets a character claim a configured starter package once per account.")]
[ChatCommandHelp(Command, "Claims the starter package for the current character.", typeof(EmptyChatCommandArgs))]
public class ClaimStarterPackageChatCommandPlugIn : ChatCommandPlugInBase<EmptyChatCommandArgs>, ISupportCustomConfiguration<ClaimStarterPackageChatCommandPlugIn.StarterPackageConfiguration>, ISupportDefaultCustomConfiguration
{
    private const string Command = "/starter";
    private const byte SwordsGroup = 0;
    private const byte AxesGroup = 1;
    private const byte BowsGroup = 4;
    private const byte StaffGroup = 5;
    private const byte Misc2Group = 14;

    private const byte DarkWizardClassNumber = 0;
    private const byte DarkKnightClassNumber = 4;
    private const byte FairyElfClassNumber = 8;
    private const byte MagicGladiatorClassNumber = 12;
    private const byte DarkLordClassNumber = 16;
    private const byte SummonerClassNumber = 20;
    private const byte RageFighterClassNumber = 24;

    private const ushort EnergyBallSkillNumber = 17;
    private const ushort LanceSkillNumber = 45;
    private const ushort StarfallSkillNumber = 46;
    private const ushort ChargeSkillNumber = 269;

    /// <inheritdoc />
    public StarterPackageConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return new StarterPackageConfiguration
        {
            Packages =
            [
                StarterPackage.ForAll(
                    money: 50_000,
                    items:
                    [
                        StarterItem.Create(Misc2Group, 1, durability: 10),
                        StarterItem.Create(Misc2Group, 4, durability: 10),
                    ]),
                StarterPackage.ForClass(DarkKnightClassNumber, items: [StarterItem.Create(AxesGroup, 0, level: 3)]),
                StarterPackage.ForClass(DarkWizardClassNumber, items: [StarterItem.Create(StaffGroup, 0, level: 3)], skills: [StarterSkill.Create(EnergyBallSkillNumber)]),
                StarterPackage.ForClass(FairyElfClassNumber, items: [StarterItem.Create(BowsGroup, 0, level: 3), StarterItem.Create(BowsGroup, 15, durability: 255)], skills: [StarterSkill.Create(StarfallSkillNumber)]),
                StarterPackage.ForClass(MagicGladiatorClassNumber, items: [StarterItem.Create(SwordsGroup, 1, level: 3)]),
                StarterPackage.ForClass(DarkLordClassNumber, items: [StarterItem.Create(SwordsGroup, 1, level: 3)]),
                StarterPackage.ForClass(SummonerClassNumber, items: [StarterItem.Create(StaffGroup, 14, level: 3)], skills: [StarterSkill.Create(LanceSkillNumber)]),
                StarterPackage.ForClass(RageFighterClassNumber, items: [StarterItem.Create(SwordsGroup, 32, level: 3)], skills: [StarterSkill.Create(ChargeSkillNumber)]),
            ],
        };
    }

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, EmptyChatCommandArgs arguments)
    {
        using var logScope = player.Logger.BeginScope(this.GetType());
        if (player.Account is not { } account || account.HasReceivedStarterPackage)
        {
            await player.ShowBlueMessageAsync("This account has already claimed its starter package.").ConfigureAwait(false);
            return;
        }

        var configuration = this.Configuration ??= (StarterPackageConfiguration)this.CreateDefaultConfig();
        if (!configuration.IsEnabled)
        {
            await player.ShowBlueMessageAsync("Starter package claiming is currently disabled.").ConfigureAwait(false);
            return;
        }

        if (player.SelectedCharacter is not { } createdCharacter)
        {
            await player.ShowBlueMessageAsync("You need to enter the game with a character before claiming the starter package.").ConfigureAwait(false);
            return;
        }

        if (createdCharacter.CharacterClass?.Number is not { } characterClassNumber)
        {
            player.Logger.LogWarning("The starter package could not be added because the character has no class.");
            await player.ShowBlueMessageAsync("The starter package could not be claimed because this character has no class.").ConfigureAwait(false);
            return;
        }

        if (createdCharacter.Inventory is null)
        {
            player.Logger.LogWarning("The starter package could not be added because the character has no inventory.");
            await player.ShowBlueMessageAsync("The starter package could not be claimed because this character has no inventory.").ConfigureAwait(false);
            return;
        }

        configuration.MigrateLegacyPackages();
        var packages = configuration.GetPackagesForCharacterClass(characterClassNumber).ToList();
        var items = packages.SelectMany(package => package.Items).ToList();
        var skills = packages.SelectMany(package => package.Skills).ToList();
        var money = packages.Sum(package => package.Money);
        if (items.Count == 0 && skills.Count == 0 && money <= 0)
        {
            player.Logger.LogDebug("No starter package configured for character class {CharacterClassNumber}.", characterClassNumber);
            await player.ShowBlueMessageAsync("There is no starter package configured for this character class.").ConfigureAwait(false);
            return;
        }

        if (!this.TryAddPackage(player, createdCharacter, items, skills, money))
        {
            await player.ShowBlueMessageAsync("The starter package could not be claimed. Please make sure you have enough inventory space.").ConfigureAwait(false);
            return;
        }

        account.HasReceivedStarterPackage = true;
        await player.SaveProgressAsync().ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IUpdateInventoryListPlugIn>(p => p.UpdateInventoryListAsync()).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<ISkillListViewPlugIn>(p => p.UpdateSkillListAsync()).ConfigureAwait(false);
        await player.ShowBlueMessageAsync("Starter package claimed successfully.").ConfigureAwait(false);
    }

    private bool TryAddPackage(Player player, Character createdCharacter, IReadOnlyList<StarterItem> items, IReadOnlyList<StarterSkill> skills, int money)
    {
        var addedItems = new List<Item>();
        var addedSkills = new List<SkillEntry>();
        var storage = new Storage(InventoryConstants.GetInventorySize(0), InventoryConstants.EquippableSlotsCount, 0, createdCharacter.Inventory!);

        foreach (var itemConfiguration in items)
        {
            if (this.CreateItem(player, itemConfiguration) is not { } item)
            {
                this.Rollback(player, createdCharacter, addedItems, addedSkills);
                return false;
            }

            if (!storage.AddItemAsync(item).AsTask().GetAwaiter().GetResult())
            {
                player.Logger.LogWarning("The starter item {Group}/{Number} does not fit into the inventory of {CharacterName}.", itemConfiguration.Group, itemConfiguration.Number, createdCharacter.Name);
                player.PersistenceContext.Detach(item);
                this.Rollback(player, createdCharacter, addedItems, addedSkills);
                return false;
            }

            addedItems.Add(item);
        }

        foreach (var skillConfiguration in skills)
        {
            if (this.CreateSkillEntry(player, createdCharacter, skillConfiguration) is not { } skillEntry)
            {
                this.Rollback(player, createdCharacter, addedItems, addedSkills);
                return false;
            }

            if (skillEntry.Skill is not null && createdCharacter.LearnedSkills.Any(entry => entry.Skill == skillEntry.Skill))
            {
                player.PersistenceContext.Detach(skillEntry);
                continue;
            }

            if (skillEntry.Skill is not null)
            {
                createdCharacter.LearnedSkills.Add(skillEntry);
                addedSkills.Add(skillEntry);
            }
        }

        if (money > 0)
        {
            var currentMoney = createdCharacter.Inventory!.Money;
            var maximumMoney = player.GameContext.Configuration.MaximumInventoryMoney;
            if (currentMoney + money > maximumMoney)
            {
                player.Logger.LogWarning("The starter money does not fit into the inventory of {CharacterName}.", createdCharacter.Name);
                this.Rollback(player, createdCharacter, addedItems, addedSkills);
                return false;
            }

            createdCharacter.Inventory.Money = checked(currentMoney + money);
        }

        return true;
    }

    private Item? CreateItem(Player player, StarterItem itemConfiguration)
    {
        if (player.GameContext.Configuration.Items.FirstOrDefault(def => def.Group == itemConfiguration.Group && def.Number == itemConfiguration.Number) is not { } itemDefinition)
        {
            player.Logger.LogWarning("Unknown starter item, group {Group}, number {Number}.", itemConfiguration.Group, itemConfiguration.Number);
            return null;
        }

        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = itemDefinition;
        item.Durability = itemConfiguration.Durability ?? item.Definition.Durability;
        item.Level = itemConfiguration.Level;
        return item;
    }

    private SkillEntry? CreateSkillEntry(Player player, Character createdCharacter, StarterSkill skillConfiguration)
    {
        if (createdCharacter.CharacterClass is not { } characterClass)
        {
            player.Logger.LogWarning("The character {CharacterName} has no assigned character class.", createdCharacter.Name);
            return null;
        }

        var skillDefinition = player.GameContext.Configuration.Skills.FirstOrDefault(s => s.Number == skillConfiguration.Number);
        if (skillDefinition is null)
        {
            player.Logger.LogWarning("Unknown starter skill, number {SkillNumber}.", skillConfiguration.Number);
            return null;
        }

        if (!skillDefinition.QualifiedCharacters.Contains(characterClass))
        {
            player.Logger.LogWarning("Starter skill {SkillName} is not available for character class {CharacterClassName}.", skillDefinition.Name, characterClass.Name);
            return null;
        }

        var skillEntry = player.PersistenceContext.CreateNew<SkillEntry>();
        skillEntry.Skill = skillDefinition;
        return skillEntry;
    }

    private void Rollback(Player player, Character createdCharacter, IEnumerable<Item> addedItems, IEnumerable<SkillEntry> addedSkills)
    {
        foreach (var item in addedItems)
        {
            createdCharacter.Inventory?.Items.Remove(item);
            player.PersistenceContext.Detach(item);
        }

        foreach (var skill in addedSkills)
        {
            createdCharacter.LearnedSkills.Remove(skill);
            player.PersistenceContext.Detach(skill);
        }
    }

    /// <summary>
    /// The starter package configuration.
    /// </summary>
    public class StarterPackageConfiguration : IJsonOnDeserialized
    {
        /// <summary>
        /// Gets or sets a value indicating whether the starter package is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the configured starter packages.
        /// </summary>
        [ScaffoldColumn(true)]
        [MemberOfAggregate]
        public ICollection<StarterPackage> Packages { get; set; } = new List<StarterPackage>();

        /// <summary>
        /// Gets or sets the legacy money which is migrated into the common package.
        /// </summary>
        [Browsable(false)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Money { get; set; }

        /// <summary>
        /// Gets or sets the legacy starter items which are migrated into grouped packages.
        /// </summary>
        [Browsable(false)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ICollection<StarterItem>? Items { get; set; }

        /// <summary>
        /// Gets or sets the legacy starter skills which are migrated into grouped packages.
        /// </summary>
        [Browsable(false)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ICollection<StarterSkill>? Skills { get; set; }

        /// <inheritdoc />
        public void OnDeserialized()
        {
            this.MigrateLegacyPackages();
        }

        /// <summary>
        /// Gets the packages which apply to the specified character class.
        /// </summary>
        /// <param name="characterClassNumber">The character class number.</param>
        /// <returns>The matching packages.</returns>
        internal IEnumerable<StarterPackage> GetPackagesForCharacterClass(int characterClassNumber)
        {
            return this.Packages.Where(package => package.IsForCharacterClass(characterClassNumber));
        }

        /// <summary>
        /// Migrates the previous flat configuration fields into grouped starter packages.
        /// </summary>
        internal void MigrateLegacyPackages()
        {
            var legacyItems = this.Items ?? [];
            var legacySkills = this.Skills ?? [];
            if (this.Packages.Count > 0 || (this.Money <= 0 && legacyItems.Count == 0 && legacySkills.Count == 0))
            {
                return;
            }

            var packages = new List<StarterPackage>();

            StarterPackage GetOrCreatePackage(int? characterClassNumber)
            {
                if (packages.FirstOrDefault(p => p.CharacterClassNumber == characterClassNumber) is { } package)
                {
                    return package;
                }

                package = new StarterPackage { CharacterClassNumber = characterClassNumber };
                packages.Add(package);
                return package;
            }

            if (this.Money > 0)
            {
                GetOrCreatePackage(null).Money = this.Money;
            }

            foreach (var item in legacyItems)
            {
                GetOrCreatePackage(item.CharacterClassNumber).Items.Add(item);
                item.CharacterClassNumber = null;
            }

            foreach (var skill in legacySkills)
            {
                var characterClassNumber = skill.CharacterClassNumber;
                if (characterClassNumber is null)
                {
                    continue;
                }

                GetOrCreatePackage(characterClassNumber).Skills.Add(skill);
                skill.CharacterClassNumber = null;
            }

            this.Packages = packages;
            this.Money = 0;
            this.Items = null;
            this.Skills = null;
        }
    }

    /// <summary>
    /// A configured starter package for a character class.
    /// </summary>
    public class StarterPackage
    {
        /// <summary>
        /// Gets the display name.
        /// </summary>
        [Browsable(false)]
        public string Name => this.CharacterClassNumber is null ? "All classes" : $"Class {this.CharacterClassNumber}";

        /// <summary>
        /// Gets or sets the character class number. If it's null, this package is added for every class.
        /// </summary>
        [Display(Name = "Character Class")]
        public int? CharacterClassNumber { get; set; }

        /// <summary>
        /// Gets or sets the money which is added to the receiving character inventory.
        /// </summary>
        public int Money { get; set; }

        /// <summary>
        /// Gets or sets the configured starter items.
        /// </summary>
        [ScaffoldColumn(true)]
        [MemberOfAggregate]
        public ICollection<StarterItem> Items { get; set; } = new List<StarterItem>();

        /// <summary>
        /// Gets or sets the configured starter skills.
        /// </summary>
        [ScaffoldColumn(true)]
        [MemberOfAggregate]
        public ICollection<StarterSkill> Skills { get; set; } = new List<StarterSkill>();

        /// <summary>
        /// Creates a starter package for a specific character class.
        /// </summary>
        /// <param name="characterClassNumber">The character class number.</param>
        /// <param name="items">The starter items.</param>
        /// <param name="skills">The starter skills.</param>
        /// <param name="money">The starter money.</param>
        /// <returns>The starter package.</returns>
        internal static StarterPackage ForClass(byte characterClassNumber, ICollection<StarterItem>? items = null, ICollection<StarterSkill>? skills = null, int money = 0)
        {
            return new StarterPackage { CharacterClassNumber = characterClassNumber, Money = money, Items = items ?? [], Skills = skills ?? [] };
        }

        /// <summary>
        /// Creates a starter package which applies to all character classes.
        /// </summary>
        /// <param name="items">The starter items.</param>
        /// <param name="skills">The starter skills.</param>
        /// <param name="money">The starter money.</param>
        /// <returns>The starter package.</returns>
        internal static StarterPackage ForAll(ICollection<StarterItem>? items = null, ICollection<StarterSkill>? skills = null, int money = 0)
        {
            return new StarterPackage { Money = money, Items = items ?? [], Skills = skills ?? [] };
        }

        /// <summary>
        /// Determines whether this package applies to the specified character class.
        /// </summary>
        /// <param name="characterClassNumber">The character class number.</param>
        /// <returns><c>true</c> if this package applies; otherwise, <c>false</c>.</returns>
        internal bool IsForCharacterClass(int characterClassNumber)
        {
            return this.CharacterClassNumber is null || this.CharacterClassNumber == characterClassNumber;
        }
    }

    /// <summary>
    /// A configured starter item.
    /// </summary>
    public class StarterItem
    {
        /// <summary>
        /// Gets the display name.
        /// </summary>
        [Browsable(false)]
        public string Name => $"Item {this.Group}/{this.Number} +{this.Level}";

        /// <summary>
        /// Gets or sets the legacy character class number.
        /// </summary>
        [Browsable(false)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CharacterClassNumber { get; set; }

        /// <summary>
        /// Gets or sets the item group.
        /// </summary>
        public byte Group { get; set; }

        /// <summary>
        /// Gets or sets the item number.
        /// </summary>
        public byte Number { get; set; }

        /// <summary>
        /// Gets or sets the item level.
        /// </summary>
        public byte Level { get; set; }

        /// <summary>
        /// Gets or sets the item durability. If it's null, the item definition durability is used.
        /// </summary>
        public byte? Durability { get; set; }

        /// <summary>
        /// Creates a starter item.
        /// </summary>
        /// <param name="group">The item group.</param>
        /// <param name="number">The item number.</param>
        /// <param name="level">The item level.</param>
        /// <param name="durability">The item durability.</param>
        /// <returns>The starter item.</returns>
        internal static StarterItem Create(byte group, byte number, byte level = 0, byte? durability = null)
        {
            return new StarterItem { Group = group, Number = number, Level = level, Durability = durability };
        }
    }

    /// <summary>
    /// A configured starter skill.
    /// </summary>
    public class StarterSkill
    {
        /// <summary>
        /// Gets the display name.
        /// </summary>
        [Browsable(false)]
        public string Name => $"Skill {this.Number}";

        /// <summary>
        /// Gets or sets the legacy character class number.
        /// </summary>
        [Browsable(false)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CharacterClassNumber { get; set; }

        /// <summary>
        /// Gets or sets the skill number.
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// Creates a starter skill.
        /// </summary>
        /// <param name="skillNumber">The skill number.</param>
        /// <returns>The starter skill.</returns>
        internal static StarterSkill Create(int skillNumber)
        {
            return new StarterSkill { Number = skillNumber };
        }
    }
}
