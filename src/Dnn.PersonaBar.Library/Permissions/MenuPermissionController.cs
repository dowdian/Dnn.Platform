#region Copyright
// 
// DotNetNukeŽ - http://www.dotnetnuke.com
// Copyright (c) 2002-2016
// by DotNetNuke Corporation
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
// documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
// to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions 
// of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
// TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
// CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.
#endregion
#region Usings

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Caching;
using Dnn.PersonaBar.Library.Data;
using Dnn.PersonaBar.Library.Model;
using Dnn.PersonaBar.Library.Repository;
using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Users;
using DotNetNuke.Instrumentation;
using DotNetNuke.Security;
using DotNetNuke.Security.Permissions;
using DotNetNuke.Security.Roles;
using PermissionInfo = Dnn.PersonaBar.Library.Model.PermissionInfo;

#endregion

namespace Dnn.PersonaBar.Library.Permissions
{
    public class MenuPermissionController
    {
        #region Private Members

        private static readonly DnnLogger Logger = DnnLogger.GetClassLogger(typeof(MenuPermissionController));

        private static readonly PermissionProvider _provider = PermissionProvider.Instance();
        private static IDataService _dataService = new DataService();
        private static object _threadLocker = new object();
        private static object _defaultPermissionLocker = new object();

        private const string PersonaBarMenuPermissionsCacheKey = "PersonaBarMenuPermissions{0}";
        private const string PersonaBarPermissionsCacheKey = "PersonaBarPermissions";
        private const string PermissionInitializedKey = "PersonaBarMenuPermissionsInitialized";

        private const string ViewPermissionKey = "VIEW";

        #endregion

        #region Public Methods

        public static bool CanView(int portalId, MenuItem menu)
        {
            return HasMenuPermission(GetMenuPermissions(portalId, menu.MenuId), ViewPermissionKey);
        }

        public static void DeleteMenuPermissions(int portalId, MenuItem menu)
        {
            _dataService.DeletePersonbaBarMenuPermissionsByMenuId(portalId, menu.MenuId);
            ClearCache(portalId);
        }

        public static MenuPermissionCollection GetMenuPermissions(int portalId)
        {
            var cacheKey = GetCacheKey(portalId);
            var permissions = DataCache.GetCache<MenuPermissionCollection>(cacheKey);
            if (permissions == null)
            {
                lock (_threadLocker)
                {
                    permissions = DataCache.GetCache<MenuPermissionCollection>(cacheKey);
                    if (permissions == null)
                    {
                        permissions = new MenuPermissionCollection();
                        EnsureMenuDefaultPermissions(portalId);
                        var reader = _dataService.GetPersonbaBarMenuPermissionsByPortal(portalId);
                        try
                        {
                            while (reader.Read())
                            {
                                var permissionInfo = CBO.FillObject<MenuPermissionInfo>(reader, false);
                                permissions.Add(permissionInfo, true);
                            }

                            DataCache.SetCache(cacheKey, permissions);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex);
                        }
                        finally
                        {
                            CBO.CloseDataReader(reader, true);
                        }
                    }
                }
            }

            return permissions;
        }

        public static MenuPermissionCollection GetMenuPermissions(int portalId, string identifier)
        {
            var menu = PersonaBarRepository.Instance.GetMenuItem(identifier);
            if (menu == null)
            {
                return null;
            }

            return GetMenuPermissions(portalId, menu.MenuId);
        }

        public static MenuPermissionCollection GetMenuPermissions(int portalId, int menuId)
        {
            var permissions = GetMenuPermissions(portalId)
                    .Cast<MenuPermissionInfo>()
                    .Where(p => p.MenuId == menuId && (p.PortalId == Null.NullInteger || p.PortalId == portalId)).ToList();
            return new MenuPermissionCollection(permissions);
        }

        public static bool HasMenuPermission(int portalId, MenuItem menu, string permissionKey)
        {
            return HasMenuPermission(GetMenuPermissions(portalId, menu.MenuId), permissionKey);
        }

        public static bool HasMenuPermission(MenuPermissionCollection menuPermissions, string permissionKey)
        {
            bool hasPermission = Null.NullBoolean;
            if (permissionKey.Contains(","))
            {
                foreach (string permission in permissionKey.Split(','))
                {
                    if (PortalSecurity.IsInRoles(menuPermissions.ToString(permission)))
                    {
                        hasPermission = true;
                        break;
                    }
                }
            }
            else
            {
                hasPermission = PortalSecurity.IsInRoles(menuPermissions.ToString(permissionKey));
            }
            return hasPermission;
        }

        public static void SaveMenuPermissions(int portalId, MenuItem menu, MenuPermissionInfo permissionInfo)
        {
            var user = UserController.Instance.GetCurrentUserInfo();

            permissionInfo.MenuPermissionId = _dataService.SavePersonaBarMenuPermission(
                portalId, 
                menu.MenuId, 
                permissionInfo.PermissionID,
                permissionInfo.RoleID, 
                permissionInfo.UserID, 
                permissionInfo.AllowAccess, 
                user.UserID);

            ClearCache(portalId);
        }

        public static IList<PermissionInfo> GetPermissions(int menuId)
        {
            return GetAllPermissions()
                .Where(p => p.MenuId == Null.NullInteger || p.MenuId == menuId)
                .ToList();
        }

        public static void SaveMenuDefaultPermissions(int portalId, MenuItem menuItem, string roleName)
        {
            SaveMenuDefaultPermissions(portalId, menuItem, roleName, false);
        }

        public static void SavePersonaBarPermission(string menuIdentifier, string permissionKey, string permissionName)
        {
            var menu = PersonaBarRepository.Instance.GetMenuItem(menuIdentifier);
            if (menu == null)
            {
                return;
            }

            SavePersonaBarPermission(menu.MenuId, permissionKey, permissionName);
        }

        public static void SavePersonaBarPermission(int menuId, string permissionKey, string permissionName)
        {
            var user = UserController.Instance.GetCurrentUserInfo();

            _dataService.SavePersonaBarPermission(menuId, permissionKey, permissionName, user.UserID);

            ClearCache(Null.NullInteger);
        }

        public static void DeletePersonaBarPermission(int menuId, string permissionKey)
        {
            var permission = GetAllPermissions().FirstOrDefault(p => p.MenuId == menuId && p.PermissionKey == permissionKey);

            if (permission != null)
            {
                _dataService.DeletePersonaBarPermission(permission.PermissionId);
            }

            ClearCache(Null.NullInteger);
        }

        public static bool PermissionAlreadyInitialized(int portalId)
        {
            return PortalController.Instance.GetPortalSettings(portalId).ContainsKey(PermissionInitializedKey);
        }

        #endregion

        #region Private Methods

        private static void SetPermissionIntialized(int portalId)
        {
            PortalController.UpdatePortalSetting(portalId, PermissionInitializedKey, "Y");
        }

        private static void EnsureMenuDefaultPermissions(int portalId)
        {
            try
            {
                var permissionInitialized = PermissionAlreadyInitialized(portalId);
                if (!permissionInitialized)
                {
                    lock (_defaultPermissionLocker)
                    {
                        permissionInitialized = PermissionAlreadyInitialized(portalId);
                        if (!permissionInitialized)
                        {
                            var menuItems = PersonaBarRepository.Instance.GetMenu().AllItems;
                            foreach (var menuItem in menuItems)
                            {
                                var defaultPermissions = PersonaBarRepository.Instance.GetMenuDefaultPermissions(menuItem.MenuId);
                                if (!string.IsNullOrEmpty(defaultPermissions))
                                {
                                    foreach (var roleName in defaultPermissions.Split(','))
                                    {
                                        if (!string.IsNullOrEmpty(roleName.Trim()))
                                        {
                                            SaveMenuDefaultPermissions(portalId, menuItem, roleName.Trim(), true);
                                        }
                                    }
                                }
                            }

                            SetPermissionIntialized(portalId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

        }

        private static void SaveMenuDefaultPermissions(int portalId, MenuItem menuItem, string roleName, bool ignoreExists)
        {
            try
            {
                var defaultPermissions = roleName.Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (defaultPermissions.Count > 1)
                {
                    roleName = defaultPermissions[0];
                }
                defaultPermissions.RemoveAt(0);

                var nullRoleId = Convert.ToInt32(Globals.glbRoleNothing);
                var permissions = GetPermissions(menuItem.MenuId)
                    .Where(p => p.MenuId == Null.NullInteger || defaultPermissions.Contains(p.PermissionKey));

                var roleId = nullRoleId;
                switch (roleName)
                {
                    case Globals.glbRoleUnauthUserName:
                        roleId = Convert.ToInt32(Globals.glbRoleUnauthUser);
                        break;
                    case Globals.glbRoleAllUsersName:
                        roleId = Convert.ToInt32(Globals.glbRoleAllUsers);
                        break;
                    default:
                        var role = RoleController.Instance.GetRoleByName(portalId, roleName);
                        if (role != null && role.IsSystemRole)
                        {
                            roleId = role.RoleID;
                        }
                        break;
                }
                
                if (roleId > nullRoleId && 
                        (ignoreExists || GetMenuPermissions(portalId, menuItem.MenuId).ToList().All(p => p.RoleID != roleId)))
                {
                    foreach (var permission in permissions)
                    {
                        var menuPermissionInfo = new MenuPermissionInfo
                        {
                            MenuPermissionId = Null.NullInteger,
                            MenuId = menuItem.MenuId,
                            PermissionID = permission.PermissionId,
                            RoleID = roleId,
                            UserID = Null.NullInteger,
                            AllowAccess = true
                        };

                        SaveMenuPermissions(portalId, menuItem, menuPermissionInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private static void ClearCache(int portalId)
        {
            if (portalId > Null.NullInteger)
            {
                var cacheKey = GetCacheKey(portalId);
                DataCache.RemoveCache(cacheKey);
            }
            else
            {
                DataCache.RemoveCache(PersonaBarPermissionsCacheKey);
            }
        }

        private static string GetCacheKey(int portalId)
        {
            return string.Format(PersonaBarMenuPermissionsCacheKey, portalId);
        }

        private static IList<PermissionInfo> GetAllPermissions()
        {
            var cacheItemArgs = new CacheItemArgs(PersonaBarPermissionsCacheKey, 20, CacheItemPriority.AboveNormal);
            return CBO.GetCachedObject<IList<PermissionInfo>>(cacheItemArgs, c =>
                CBO.FillCollection<PermissionInfo>(_dataService.GetPersonaBarPermissions()));
        }

        #endregion
    }
}
