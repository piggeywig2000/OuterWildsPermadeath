using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using OWML.Common.Menus;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Permadeath.Patches
{
    [HarmonyPatch(typeof(StandaloneProfileManager))]
    internal static class TitleScreenPatches
    {
        public static event EventHandler OnInitializeProfileData;
        public static event EventHandler OnPopupReady;

        [HarmonyPatch(nameof(StandaloneProfileManager.InitializeProfileData))]
        [HarmonyPostfix]
        public static void InitializeProfileDataPostfix()
        {
            OnInitializeProfileData?.Invoke(null, EventArgs.Empty);
        }

        [HarmonyPatch(nameof(StandaloneProfileManager.DeleteProfile))]
        [HarmonyPostfix]
        public static void DeleteProfilePostfix(string profileName)
        {
            SaveManager.Data.RemoveProfile(profileName);
            SaveManager.Save();
        }

        [HarmonyPatch(typeof(TitleScreenManager), nameof(TitleScreenManager.OnNewGameSubmit))]
        [HarmonyPostfix]
        public static void OnNewGameSubmitPostfix()
        {
            SaveManager.Data.GetOrAddCurrentProfile().PermadeathEnabled = false;
            SaveManager.Save();
        }

        [HarmonyPatch(typeof(TitleScreenManager), nameof(TitleScreenManager.OnUserConfirmStartupPopup))]
        [HarmonyPostfix]
        public static void OnUserConfirmStartupPopupPostfix(TitleScreenManager __instance)
        {
            if (__instance._popupsToShow == StartupPopups.None)
                OnPopupReady?.Invoke(null, EventArgs.Empty);
        }
    }
}
