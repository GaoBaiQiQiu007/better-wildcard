using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace EnhancedWildcardGivePlugin
{
    [ApiVersion(2, 1)]
    public class EnhancedWildcardGivePlugin : TerrariaPlugin
    {
        public override string Name => "bettergive";
        public override string Author => "ak";
        public override string Description => "支持全体玩家、多目标、排除目标、me、@a、物品名空格的发物品命令";
        public override Version Version => new Version(3, 0, 1, 0);

        private const string GivePermission = "pw.give";
        private const string GBoxPermission = "pw.gbox";

        public EnhancedWildcardGivePlugin(Main game) : base(game)
        {
        }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command(GivePermission, WGiveCommand, "wgive"));
            Commands.ChatCommands.Add(new Command(GBoxPermission, WGBoxCommand, "wgbox"));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Commands.ChatCommands.RemoveAll(c => c.CommandDelegate == WGiveCommand);
                Commands.ChatCommands.RemoveAll(c => c.CommandDelegate == WGBoxCommand);
            }

            base.Dispose(disposing);
        }

        private void WGiveCommand(CommandArgs args)
        {
            if (args.Parameters.Count < 3)
            {
                args.Player.SendErrorMessage("用法: /wgive <物品ID或名称...> <玩家|*|all|@a|me|玩家1,玩家2,!玩家3> <数量>");
                return;
            }

            string amountText = args.Parameters[args.Parameters.Count - 1];
            string targetText = args.Parameters[args.Parameters.Count - 2];
            string itemText = string.Join(" ", args.Parameters.Take(args.Parameters.Count - 2));

            if (!TryParsePositiveInt(amountText, out int amount))
            {
                args.Player.SendErrorMessage("数量必须是大于 0 的整数。");
                return;
            }

            if (!TryResolveSingleItem(args.Player, itemText, out Item item))
                return;

            if (!TryResolveTargets(args.Player, targetText, out List<TSPlayer> targets))
                return;

            int success = 0;
            foreach (TSPlayer target in targets)
            {
                if (TryGiveItem(target, item.type, amount, 0))
                {
                    target.SendSuccessMessage($"你收到了 {item.Name} x{amount}");
                    success++;
                }
            }

            args.Player.SendSuccessMessage($"已向 {success} 名玩家发放 {item.Name} x{amount}");
        }

        private void WGBoxCommand(CommandArgs args)
        {
            if (args.Parameters.Count < 3)
            {
                args.Player.SendErrorMessage("用法: /wgbox <玩家|*|all|@a|me|玩家1,玩家2,!玩家3> <物品ID或名称...> <数量> [词缀]");
                return;
            }

            string targetText = args.Parameters[0];
            int amount;
            byte prefix = 0;
            string itemText;

            if (args.Parameters.Count >= 4 && byte.TryParse(args.Parameters[args.Parameters.Count - 1], out byte parsedPrefix))
            {
                if (!TryParsePositiveInt(args.Parameters[args.Parameters.Count - 2], out amount))
                {
                    args.Player.SendErrorMessage("数量必须是大于 0 的整数。");
                    return;
                }

                prefix = parsedPrefix;
                itemText = string.Join(" ", args.Parameters.Skip(1).Take(args.Parameters.Count - 3));
            }
            else
            {
                if (!TryParsePositiveInt(args.Parameters[args.Parameters.Count - 1], out amount))
                {
                    args.Player.SendErrorMessage("数量必须是大于 0 的整数。");
                    return;
                }

                itemText = string.Join(" ", args.Parameters.Skip(1).Take(args.Parameters.Count - 2));
            }

            if (string.IsNullOrWhiteSpace(itemText))
            {
                args.Player.SendErrorMessage("物品名称不能为空。");
                return;
            }

            if (!TryResolveSingleItem(args.Player, itemText, out Item item))
                return;

            if (!TryResolveTargets(args.Player, targetText, out List<TSPlayer> targets))
                return;

            int success = 0;
            foreach (TSPlayer target in targets)
            {
                if (TryGiveItem(target, item.type, amount, prefix))
                {
                    if (prefix > 0)
                        target.SendSuccessMessage($"你收到了 {item.Name} x{amount}，词缀: {prefix}");
                    else
                        target.SendSuccessMessage($"你收到了 {item.Name} x{amount}");

                    success++;
                }
            }

            if (prefix > 0)
                args.Player.SendSuccessMessage($"已向 {success} 名玩家发放 {item.Name} x{amount}，词缀: {prefix}");
            else
                args.Player.SendSuccessMessage($"已向 {success} 名玩家发放 {item.Name} x{amount}");
        }

        private static bool TryParsePositiveInt(string text, out int value)
        {
            value = 0;
            return int.TryParse(text, out value) && value > 0;
        }

        private bool TryResolveSingleItem(TSPlayer player, string itemText, out Item item)
        {
            item = new Item();

            if (string.IsNullOrWhiteSpace(itemText))
            {
                player.SendErrorMessage("物品名称不能为空。");
                return false;
            }

            List<Item> found = TShock.Utils.GetItemByIdOrName(itemText);

            if (found == null || found.Count == 0)
            {
                player.SendErrorMessage($"未找到物品: {itemText}");
                return false;
            }

            if (found.Count > 1)
            {
                player.SendMultipleMatchError(found.Select(i => $"{i.Name} ({i.type})"));
                return false;
            }

            item = found[0];
            return true;
        }

        private bool TryResolveTargets(TSPlayer sender, string targetText, out List<TSPlayer> targets)
        {
            targets = new List<TSPlayer>();

            if (string.IsNullOrWhiteSpace(targetText))
            {
                sender.SendErrorMessage("目标不能为空。");
                return false;
            }

            HashSet<TSPlayer> include = new HashSet<TSPlayer>();
            HashSet<TSPlayer> exclude = new HashSet<TSPlayer>();

            string[] parts = targetText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string raw in parts)
            {
                string token = raw.Trim();
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                bool isExclude = token.StartsWith("!");
                string actualToken = isExclude ? token.Substring(1).Trim() : token;

                if (string.IsNullOrWhiteSpace(actualToken))
                    continue;

                List<TSPlayer> resolved = ResolveTargetToken(sender, actualToken);
                if (resolved.Count == 0)
                    continue;

                if (isExclude)
                {
                    foreach (TSPlayer player in resolved)
                        exclude.Add(player);
                }
                else
                {
                    foreach (TSPlayer player in resolved)
                        include.Add(player);
                }
            }

            if (include.Count == 0)
            {
                sender.SendErrorMessage("未找到任何有效目标。");
                return false;
            }

            foreach (TSPlayer player in include)
            {
                if (!exclude.Contains(player))
                    targets.Add(player);
            }

            if (targets.Count == 0)
            {
                sender.SendErrorMessage("目标列表为空（可能全部被排除了）。");
                return false;
            }

            return true;
        }

        private List<TSPlayer> ResolveTargetToken(TSPlayer sender, string token)
        {
            List<TSPlayer> result = new List<TSPlayer>();

            if (token == "*" ||
                token.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("@a", StringComparison.OrdinalIgnoreCase))
            {
                foreach (TSPlayer player in TShock.Players)
                {
                    if (player != null && player.Active)
                        result.Add(player);
                }

                return result;
            }

            if (token.Equals("me", StringComparison.OrdinalIgnoreCase))
            {
                if (sender != null && sender.Active)
                    result.Add(sender);

                return result;
            }

            List<TSPlayer> found = TSPlayer.FindByNameOrID(token);

            if (found == null || found.Count == 0)
            {
                sender.SendErrorMessage($"未找到玩家: {token}");
                return result;
            }

            if (found.Count > 1)
            {
                sender.SendMultipleMatchError(found.Select(p => $"{p.Name} ({p.Index})"));
                return result;
            }

            TSPlayer matched = found[0];
            if (matched != null && matched.Active)
                result.Add(matched);

            return result;
        }

        private bool TryGiveItem(TSPlayer target, int itemType, int stack, int prefix)
        {
            if (target == null || !target.Active)
                return false;

            try
            {
                target.GiveItem(itemType, stack, prefix);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
