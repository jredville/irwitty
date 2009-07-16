using System;
using System.Collections.Generic;
using System.Windows;

namespace Witty
{
    internal class SkinsManager
    {
        internal static string CurrentSkin;

        internal static List<string> GetSkins()
        {
            List<string> skins = new List<string>();

            //TODO: make this dynamic by checking against WittySkins assembly
            skins.Add("Aero");
            skins.Add("AeroCompact");
            skins.Add("CoolBlue");

            return skins;
        }

        internal static void ChangeSkin(string skin)
        {
            Uri resourceLocator = new Uri("WittySkins;Component/" + skin + ".xaml", UriKind.RelativeOrAbsolute);
            Application.Current.Resources =  Application.LoadComponent(resourceLocator) as ResourceDictionary;

            CurrentSkin = skin;
        }
    }
}
