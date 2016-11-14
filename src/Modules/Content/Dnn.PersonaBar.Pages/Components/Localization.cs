﻿using System.IO;
using Dnn.PersonaBar.Library;

namespace Dnn.PersonaBar.Pages.Components
{
    public class Localization
    {
        private static string LocalResourcesFile => Path.Combine(Constants.PersonaBarRelativePath, "App_LocalResources/Pages.resx");

        public static string GetString(string key)
        {
            return DotNetNuke.Services.Localization.Localization.GetString(key, LocalResourcesFile);
        }
    }
}