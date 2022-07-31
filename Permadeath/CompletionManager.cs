using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Permadeath
{
    public class CompletionManager : MonoBehaviour
    {
        private Text shipLogText = null;

        public event EventHandler OnChange;
        public double Completion { get; private set; } = 0;

        private void Awake()
        {
            if (LoadManager.GetCurrentScene() == OWScene.SolarSystem)
            {
                Canvas canvas = GameObject.FindGameObjectWithTag("ShipComputer").GetComponent<ShipLogController>()._shipLogCanvas;

                GameObject shipLogObject = new GameObject("PermadeathText");
                shipLogObject.transform.SetParent(canvas.transform, false);
                shipLogObject.transform.localPosition = Vector3.zero;
                shipLogObject.transform.localRotation = Quaternion.identity;
                shipLogObject.transform.localScale = Vector3.one;

                shipLogText = shipLogObject.AddComponent<Text>();
                shipLogText.rectTransform.pivot = new Vector2(0.5f, 0);
                shipLogText.rectTransform.anchorMin = new Vector2(0, 0);
                shipLogText.rectTransform.anchorMax = new Vector2(1, 0);
                shipLogText.rectTransform.anchoredPosition = new Vector2(0, 26);
                shipLogText.rectTransform.sizeDelta = new Vector2(shipLogText.rectTransform.sizeDelta.x, 60);

                SetShipLogText();
                shipLogText.font = Resources.Load<Font>("fonts/english - latin/SpaceMono-Regular");
                shipLogText.fontSize = 20;
                shipLogText.lineSpacing = 0.8f;
                shipLogText.alignment = TextAnchor.LowerCenter;
                shipLogText.alignByGeometry = true;
                shipLogText.color = Color.white;

                if (Permadeath.IsEnabled)
                {
                    GlobalMessenger.AddListener("ShipLogUpdated", Recalculate);
                }
            }
        }

        private void Start()
        {
            if (Permadeath.IsEnabled)
            {
                Recalculate();
            }
        }

        private void OnDestroy()
        {
            if (Permadeath.IsEnabled)
            {
                GlobalMessenger.RemoveListener("ShipLogUpdated", Recalculate);
            }
        }

        private void Recalculate()
        {
            bool includeDlc = EntitlementsManager.IsDlcOwned() == EntitlementsManager.AsyncOwnershipStatus.Owned &&
                (!Permadeath.SharedModHelper.Config.Settings.ContainsKey("Include DLC in progress percentage") || (bool)Permadeath.SharedModHelper.Config.Settings["Include DLC in progress percentage"]);

            ShipLogManager manager = Locator.GetShipLogManager();
            List<ShipLogFact> facts = manager.GetEntryList()
                .SelectMany(entry => entry.GetExploreFacts())
                .Where(fact => fact.GetID() != "TH_VILLAGE_X1" && fact.GetID() != "TH_VILLAGE_X2" && fact.GetID() != "TH_VILLAGE_X3" && fact.GetID() != "GD_GABBRO_ISLAND_X1" && (includeDlc || manager.GetEntry(fact.GetEntryID()).GetCuriosityName() != CuriosityName.InvisiblePlanet))
                .ToList();

            int totalFacts = facts.Count;
            int revealedFacts = facts.Count(fact => fact.IsRevealed());
            Completion = (double)revealedFacts / (double)totalFacts;
            if (LoadManager.GetCurrentScene() == OWScene.SolarSystem)
            {
                SetShipLogText();
            }

            OnChange?.Invoke(this, EventArgs.Empty);

            Permadeath.SharedModHelper.Console.WriteLine($"Total facts: {totalFacts}    Revealed facts: {revealedFacts}    Ship log completion: {Completion}", OWML.Common.MessageType.Debug);
        }

        private void SetShipLogText()
        {
            shipLogText.text = Permadeath.IsEnabled ? $"PERMADEATH: <color=red>ON</color>    PROGRESS: {Completion:P1}" : "PERMADEATH: OFF";
        }
    }
}
