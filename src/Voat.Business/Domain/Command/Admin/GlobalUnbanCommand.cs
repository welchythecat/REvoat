using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Voat.Caching;
using Voat.Common;
using Voat.Data;
using Voat.Domain.Models;
using Voat.Utilities;

namespace Voat.Domain.Command
{
    /// <summary>
    /// Removes global bans. The counterpart to GlobalBanCommand, and written
    /// to match it stage for stage so the two cannot drift apart.
    /// </summary>
    /// <remarks>
    /// Deliberately different from GlobalBanCommand in one place: unbanning a
    /// user does NOT require the account to still exist. A stale ban row on a
    /// deleted account must still be removable, and refusing that would leave
    /// the ban table permanently uncleanable from the UI.
    /// </remarks>
    public class GlobalUnbanCommand : CacheCommand<CommandResponse>
    {
        private IEnumerable<GenericReference<BanType>> _banList = null;

        public GlobalUnbanCommand(IEnumerable<GenericReference<BanType>> banItem)
        {
            _banList = banItem;
        }

        protected override async Task<CommandResponse> ExecuteStage(CommandStage stage, CommandResponse previous)
        {
            var commandResponse = CommandResponse.FromStatus(Status.Success);

            switch (stage)
            {
                case CommandStage.OnAuthorization:
                    if (!User.IsInAnyRole(new[] { UserRole.GlobalAdmin, UserRole.Admin, UserRole.DelegateAdmin, UserRole.GlobalBans }))
                    {
                        commandResponse = CommandResponse.FromStatus(Status.Denied, "Permissions not granted");
                    }
                    break;
                case CommandStage.OnValidation:
                    if (_banList == null || _banList.Count() == 0)
                    {
                        commandResponse = CommandResponse.FromStatus(Status.Invalid, "Banlist can not be null or empty");
                    }
                    else
                    {
                        foreach (var banItem in _banList)
                        {
                            switch (banItem.Type)
                            {
                                case BanType.Domain:
                                    //Same extraction as GlobalBanCommand so a
                                    //pasted URL unbans the domain it contains.
                                    var match = Regex.Match(banItem.Name, CONSTANTS.HTTP_LINK_REGEX, RegexOptions.IgnoreCase);
                                    if (match.Success)
                                    {
                                        banItem.Name = match.Groups["domain"].Value;
                                    }
                                    else
                                    {
                                        match = Regex.Match(banItem.Name, CONSTANTS.HOST_AND_PATH_LINK_REGEX, RegexOptions.IgnoreCase);
                                        if (!match.Success)
                                        {
                                            commandResponse = CommandResponse.FromStatus(Status.Invalid, $"Domain {banItem.Name} is not valid");
                                        }
                                        else
                                        {
                                            banItem.Name = match.Groups["domain"].Value;
                                        }
                                    }
                                    break;

                                case BanType.User:
                                    var result = UserDefinition.Parse(banItem.Name);
                                    if (result == null)
                                    {
                                        commandResponse = CommandResponse.FromStatus(Status.Invalid, $"UserName {banItem.Name} is not valid");
                                    }
                                    else
                                    {
                                        //Not resolved through
                                        //UserHelper.OriginalUsername — see the
                                        //remarks on this class. Matching in the
                                        //repository is case-insensitive.
                                        banItem.Name = result.Name;
                                    }
                                    break;
                            }
                        }
                    }
                    break;
            }

            return commandResponse;
        }

        protected override async Task<CommandResponse> CacheExecute()
        {
            using (var repo = new Repository(User))
            {
                var response = await repo.UnbanGlobally(_banList);
                return response;
            }
        }

        protected override void UpdateCache(CommandResponse result)
        {
            if (result.Success)
            {
                //Mandatory: QueryBannedUsers/QueryBannedDomains cache for an
                //hour, so without this eviction an unban silently takes up to
                //an hour to reach enforcement (GlobalBanRule,
                //IsUserGloballyBanned, messaging, SignalR, report submission).
                //Same keys GlobalBanCommand evicts.
                var hasDomain = _banList.Any(x => x.Type == BanType.Domain);
                var hasUser = _banList.Any(x => x.Type == BanType.User);
                if (hasDomain)
                {
                    CacheHandler.Instance.Remove(CachingKey.BannedDomains());
                }
                if (hasUser)
                {
                    CacheHandler.Instance.Remove(CachingKey.BannedUsers());
                }
            }
        }
    }
}
