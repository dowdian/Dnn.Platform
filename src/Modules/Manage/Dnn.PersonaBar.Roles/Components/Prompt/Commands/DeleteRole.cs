﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Dnn.PersonaBar.Library.Prompt;
using Dnn.PersonaBar.Library.Prompt.Attributes;
using Dnn.PersonaBar.Library.Prompt.Models;
using DotNetNuke.Common;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Users;
using DotNetNuke.Instrumentation;
using DotNetNuke.Services.Localization;

namespace Dnn.PersonaBar.Roles.Components.Prompt.Commands
{
    [ConsoleCommand("delete-role", "Deletes the specified DNN security role for this portal")]
    public class DeleteRole : ConsoleCommandBase
    {
        protected override string LocalResourceFile => Constants.LocalResourcesFile;

        private static readonly ILog Logger = LoggerSource.Instance.GetLogger(typeof(DeleteRole));
        private const string FlagId = "id";

        public int RoleId { get; private set; } = Convert.ToInt32(Globals.glbRoleNothing);

        public override void Init(string[] args, PortalSettings portalSettings, UserInfo userInfo, int activeTabId)
        {
            base.Init(args, portalSettings, userInfo, activeTabId);
            RoleId = GetFlagValue(FlagId, "Role Id", -1, true, true, true);
        }

        public override ConsoleResultModel Run()
        {
            try
            {
                KeyValuePair<HttpStatusCode, string> message;
                var roleName = RolesController.Instance.DeleteRole(PortalSettings, RoleId, out message);
                return !string.IsNullOrEmpty(roleName)
                    ? new ConsoleResultModel($"{LocalizeString("DeleteRole.Message")} '{roleName}' ({RoleId})") { Records = 1 }
                    : new ConsoleErrorResultModel(message.Value);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return new ConsoleErrorResultModel(LocalizeString("DeleteRole.Error"));
            }
        }
    }
}