using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using OWML.Common.Menus;
using UnityEngine;
using UnityEngine.UI;

namespace Permadeath.Patches
{
    [HarmonyPatch(typeof(GameOverController))]
    internal static class GameOverPatches
    {
        private static bool hasOverriddenFlashback = false;

        private static Text progressText;
        private static CanvasGroupAnimator progressAnimator;
        private static Text percentageText;
        private static CanvasGroupAnimator percentageAnimator;
        private static bool hasFadedInPercentage = false;

        public static event EventHandler OnPermadeath;

        private static IModMessagePopup popup = null;

        [HarmonyPatch(typeof(Flashback), nameof(Flashback.OnTriggerFlashback))]
        [HarmonyPrefix]
        public static bool OnTriggerFlashbackPrefix(Flashback __instance)
        {
            if (Permadeath.IsEnabled && !hasOverriddenFlashback)
            {
                DeathType lastDeathType = PlayerData.GetLastDeathType();
                if (lastDeathType != DeathType.Supernova && lastDeathType != DeathType.BigBang && lastDeathType != DeathType.Meditation && lastDeathType != DeathType.TimeLoop && lastDeathType != DeathType.Dream)
                {
                    //Trigger game over
                    __instance.gameObject.GetRequiredComponent<GameOverController>().OnTriggerDeathOutsideTimeLoop();
                }
                else
                {
                    SaveManager.Data.GetOrAddCurrentProfile().SafeQuit = true;
                    SaveManager.Save();
                    //Show popup
                    popup = Permadeath.SharedModHelper.Menus.PopupManager.CreateMessagePopup("You've reached the end of a loop.\n\nWould you like to quit now (progress will be saved) or start the next loop?", true, "NEXT LOOP", "QUIT");
                    popup.OnConfirm += ResumeLoop;
                    popup.OnCancel += ExitLoop;
                    OWTime.Pause(OWTime.PauseType.Menu);
                    OWInput.ChangeInputMode(InputMode.Menu);
                    Locator.GetSceneMenuManager().pauseMenu._isOpen = true;
                    Locator.GetSceneMenuManager().pauseMenu._isOpen = true;
                    Locator.GetPauseCommandListener().AddPauseCommandLock();
                }
                hasOverriddenFlashback = true;
                return false;
            }
            return true;
        }

        private static void ResumeLoop()
        {
            CleanupPopup();
            Flashback flashback = GameObject.FindGameObjectWithTag("FlashbackCamera").GetRequiredComponent<Flashback>();
            flashback.OnTriggerFlashback();
            hasOverriddenFlashback = false;
        }

        private static void ExitLoop()
        {
            CleanupPopup();
            TimeLoop.RestartTimeLoop();
            LoadManager.LoadScene(OWScene.TitleScreen, LoadManager.FadeType.ToBlack, 2f, true);
            hasOverriddenFlashback = false;
        }

        private static void CleanupPopup()
        {
            popup.OnConfirm -= ResumeLoop;
            popup.OnCancel -= ExitLoop;
            popup = null;
            OWTime.Unpause(OWTime.PauseType.Menu);
            OWInput.ChangeInputMode(InputMode.None);
            Locator.GetSceneMenuManager().pauseMenu._pauseMenu.EnableMenu(false);
            Locator.GetSceneMenuManager().pauseMenu._isOpen = false;
            Locator.GetPauseCommandListener().RemovePauseCommandLock();
        }

        [HarmonyPatch(nameof(GameOverController.SetupGameOverScreen))]
        [HarmonyPrefix]
        public static void SetupGameOverScreenPrefix(GameOverController __instance)
        {
            if (!Permadeath.IsEnabled) return;

            OnPermadeath?.Invoke(null, EventArgs.Empty);
            SaveManager.Data.GetOrAddCurrentProfile().SafeQuit = true;
            SaveManager.Save();

            Text death = __instance._deathText;
            death.rectTransform.SetLocalPositionY(100);
            death.rectTransform.sizeDelta = new Vector2(death.rectTransform.sizeDelta.x, 100);

            progressText = GameObject.Instantiate(death, __instance._gameOverTextCanvas.transform);
            progressText.text = "PROGRESS:";
            progressText.fontSize = 36;
            progressText.rectTransform.SetLocalPositionY(-18);
            progressText.rectTransform.sizeDelta = new Vector2(progressText.rectTransform.sizeDelta.x, 36);
            progressAnimator = progressText.GetRequiredComponent<CanvasGroupAnimator>();

            percentageText = GameObject.Instantiate(progressText, __instance._gameOverTextCanvas.transform);
            percentageText.text = Permadeath.CompletionManager.Completion.ToString("P1");
            percentageText.fontSize = 100;
            percentageText.rectTransform.SetLocalPositionY(-75);
            percentageText.rectTransform.sizeDelta = new Vector2(percentageText.rectTransform.sizeDelta.x, 100);
            percentageAnimator = percentageText.GetRequiredComponent<CanvasGroupAnimator>();
        }

        [HarmonyPatch(nameof(GameOverController.SetupGameOverScreen))]
        [HarmonyPostfix]
        public static void SetupGameOverScreenPostfix()
        {
            if (!Permadeath.IsEnabled) return;

            if (hasOverriddenFlashback) PlayerData.SetPersistentCondition("GAME_OVER_LAST_SAVE", false);
            progressAnimator.SetImmediate(0f, Vector3.one);
            percentageAnimator.SetImmediate(0f, Vector3.one);
        }

        [HarmonyPatch(nameof(GameOverController.Update))]
        [HarmonyPrefix]
        public static bool UpdatePrefix(GameOverController __instance)
        {
            if (!Permadeath.IsEnabled) return true;

            float fadeDuration = 5f;
            float progressDelay = 2f;
            float visibleDuration = 0f;
            if (!__instance._fadedInText && Time.time > __instance._gameOverTime + __instance._textFadeDelay)
            {
                __instance._textAnimator.AnimateTo(1f, Vector3.one, fadeDuration, __instance._fadeCurve);
                __instance._fadedInText = true;
            }
            else if (!hasFadedInPercentage && Time.time > __instance._gameOverTime + __instance._textFadeDelay + progressDelay)
            {
                progressAnimator.AnimateTo(1f, Vector3.one, fadeDuration, __instance._fadeCurve);
                percentageAnimator.AnimateTo(1f, Vector3.one, fadeDuration, __instance._fadeCurve);
                hasFadedInPercentage = true;
            }
            else if (!__instance._fadedOutText && Time.time > __instance._gameOverTime + __instance._textFadeDelay + progressDelay + fadeDuration + visibleDuration)
            {
                __instance._textAnimator.AnimateTo(0f, Vector3.one, fadeDuration, __instance._fadeCurve, invertCurve: true);
                progressAnimator.AnimateTo(0f, Vector3.one, fadeDuration, __instance._fadeCurve, invertCurve: true);
                percentageAnimator.AnimateTo(0f, Vector3.one, fadeDuration, __instance._fadeCurve, invertCurve: true);
                __instance._fadedOutText = true;
            }
            else if (__instance._fadedOutText && __instance._textAnimator.IsComplete() && !__instance._loading)
            {
                LoadManager.LoadScene(hasOverriddenFlashback ? OWScene.SolarSystem : OWScene.Credits_Fast, LoadManager.FadeType.None, 1f, true);
                hasOverriddenFlashback = false;
                __instance._loading = true;
            }

            if (Time.time > __instance._gameOverTime + __instance._textFadeDelay + progressDelay)
            {
                float duration = fadeDuration + visibleDuration;
                float startDelay = fadeDuration / 2;
                float t = Mathf.Clamp01((Time.time - __instance._gameOverTime - progressDelay - startDelay) / duration);
                percentageText.text = (((Mathf.Cos(Mathf.PI * t) + 1) / 2) * (float)Permadeath.CompletionManager.Completion).ToString("P1");
            }

            return false;
        }

        [HarmonyPatch(nameof(GameOverController.FontSizeFitting))]
        [HarmonyPrefix]
        public static bool FontSizeFittingPrefix(GameOverController __instance)
        {
            if (!Permadeath.IsEnabled) return true;

            if (!FontSizeFittingSingleText(__instance._deathText)) return false;
            if (!FontSizeFittingSingleText(progressText)) return false;
            if (!FontSizeFittingSingleText(percentageText)) return false;

            __instance._updatingCanvases = false;
            Canvas.willRenderCanvases -= __instance.FontSizeFitting;

            __instance._textAnimator.SetImmediate(0f, Vector3.one);
            progressAnimator.SetImmediate(0f, Vector3.one);
            percentageAnimator.SetImmediate(0f, Vector3.one);

            __instance._gameOverTime = Time.time;
            hasFadedInPercentage = false;
            __instance.enabled = true;
            return false;
        }

        private static bool FontSizeFittingSingleText(Text text)
        {
            int newFontSize = text.fontSize - 1;
            if ((text.preferredHeight > text.rectTransform.rect.height || text.preferredWidth > text.rectTransform.rect.width) && newFontSize > 0)
            {
                text.fontSize = newFontSize;
                return false;
            }
            if (newFontSize == 1)
            {
                Debug.LogWarning("GameOverController using font size of 1. Please check the input string and rect transform dimensions");
            }
            return true;
        }
    }
}
