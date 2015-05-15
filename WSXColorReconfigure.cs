using System;
using UnityEngine;

namespace WarpShip
{
	public class WSXColorReconfigure : PartModule
	{
		[KSPField]
		public string colorMode = "RedGreenBlue";

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Color")]
		public string currentColor = "Green";

		[KSPField]
		public string reconfigureEmissiveRenderer = String.Empty;

		[KSPField]
		public string reconfigureEngineRunningFX = String.Empty;


		[KSPEvent(guiActive = true, active = true, guiActiveEditor = true, guiName = "Set Color", guiActiveUnfocused = false)]
		public void ChangeColor()
		{
			if (currentColor == "Green") {
				currentColor = "Blue";
			} else if (currentColor == "Blue") {
				currentColor = "Purple";
			} else if (currentColor == "Purple") {
				currentColor = "Orange";
			} else if (currentColor == "Orange") {
				currentColor = "Red";
			} else {
				currentColor = "Green";
			}

			ColorReconfigure (false);
		}

		private void DoColorReconfigure(bool startup, string colorName, float cr, float cg, float cb)
		{
			if (reconfigureEmissiveRenderer != String.Empty) {
				MeshRenderer[] pRenderers = part.FindModelComponents<MeshRenderer>();
				for (var i = 0; i < pRenderers.Length; i++) {
					MeshRenderer mr = pRenderers [i];
					if (mr.gameObject.name == reconfigureEmissiveRenderer) {
						Color oldColor = mr.material.GetColor ("_EmissiveColor");
						Color newColor = new Color (cr, cg, cb, oldColor.a);
						mr.material.SetColor ("_EmissiveColor", newColor);
					}
				}
			}

			if (reconfigureEngineRunningFX != String.Empty) {
				ModuleEnginesFX fxEngine = part.FindModuleImplementing<ModuleEnginesFX>();
				fxEngine.runningEffectName = reconfigureEngineRunningFX + "_" + colorName;
			}
		}

		private void ColorReconfigure(bool startup)
		{
			switch (currentColor) {
				case("Red"): {
					DoColorReconfigure (startup, "red", 1.0f, 0.125f, 0.125f);
					break;
				}
				case("Green"): {
					DoColorReconfigure (startup, "green", 0.125f, 1.0f, 0.0f);
					break;
				}
				case("Blue"): {
					DoColorReconfigure (startup, "blue", 0.0f, 0.65f, 1.0f);
					break;
				}
				case("Purple"): {
					DoColorReconfigure (startup, "purple", 1.0f, 0.25f, 1.0f);
					break;
				}
				case("Orange"): {
					DoColorReconfigure (startup, "orange", 1.0f, 0.5f, 0.125f);
					break;
				}	
			}
		}
						
		public override void OnStart(StartState state)
		{
			ColorReconfigure(true);
		}

		public void FixedUpdate()
		{
			// I'm not sure why this needs to be asserted every frame but the
			// any attempt to put it in ColorReconfigure didn't disable the FX properly
			if (reconfigureEngineRunningFX != String.Empty) {
				if (currentColor != "Red") {
					part.Effect (reconfigureEngineRunningFX + "_red", 0f);
				}
				if (currentColor != "Green") {
					part.Effect (reconfigureEngineRunningFX + "_green", 0f);
				}
				if (currentColor != "Blue") {
					part.Effect (reconfigureEngineRunningFX + "_blue", 0f);
				}
				if (currentColor != "Purple") {
					part.Effect (reconfigureEngineRunningFX + "_purple", 0f);
				}
				if (currentColor != "Orange") {
					part.Effect (reconfigureEngineRunningFX + "_orange", 0f);
				}
			}
		}

	}
}

