/* This code is largely copied from RoverDude's USI_ModuleWarpEngine.cs
 * However, I (Sophia) have hacked it a great deal so any bugs
 * introduced are probably my fault. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace WarpShip
{

	public class ShipInfo
	{
		public Part ShipPart { get; set; }
		public float BreakingForce { get; set; }
		public float BreakingTorque { get; set; }
		public float CrashTolerance { get; set; }
		public RigidbodyConstraints Constraints { get; set; }
	}

	public class WarpDrive : PartModule
	{
		[KSPField]
		public string engageEffectName = "engage";
		[KSPField]
		public string disengageEffectName = "disengage";
		[KSPField]
		public string idleEffectName = "idle";
		[KSPField]
		public string warningEffectName = "warning";
		[KSPField]
		public string alertEffectName = "alert";
		[KSPField]
		public string powerEffectName = "power";
		[KSPField]
		public string flameoutEffectName = "flameout";

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Warp Factor") , UI_FloatRange(minValue = 0f, maxValue = 10f, stepIncrement = 0.5f)]
		public float displayWarpFactor = 10f;

		public float tweakFactor = 1.34f;

		[KSPField(guiActive = true, guiName = "Warp Drive", guiActiveEditor = false)]
		public string status = "inactive";

		[KSPField]
		public string deployAnimationName = "Engage";

		[KSPField]
		public string warpAnimationName = "WarpField";

		[KSPField] 
		public float WarpFactor = 15f;

		[KSPField]
		public float PlasmaMultiply = 4.0f;

		[KSPField]
		public float ElectricMultiply = 100.0f;

		[KSPField]
		public float Demasting = 10f;

		[KSPField]
		public int MaxAccelleration = 4;

		[KSPField]
		public float MinThrottle = 0.05f;

		[KSPField(isPersistant = true)] 
		public bool firstActivation = true;

		[KSPField(isPersistant = true)] 
		public bool IsDeployed = false;

		[KSPField(isPersistant = true)] 
		public bool IsActivated = false;

		[KSPField(isPersistant = true)] 
		public bool IsEnhanced = false;

		[KSPField(isPersistant = true)] 
		public int enhancerPoll = 0;

		[KSPField(isPersistant = true)] 
		public bool alertCondition = false;

		[KSPField(isPersistant = true)] 
		public float alertTimer = 0;

		[KSPField]
		public double DisruptRange = 2000.0;

		[KSPField]
		public int BubbleSize = 20;

		[KSPField(isPersistant = true)]
		public int BubbleEnhancement = 0;

		[KSPField]
		public float MinAltitude = 500000f;

		[KSPField(guiName = "Conservation", isPersistant = true, guiActiveEditor = true, guiActive = false)]
		[UI_Toggle(disabledText = "Velocity", enabledText = "Angular Momentum")]
		protected bool AMConservationMode = false;
		[KSPField(guiName = "Conservation", guiActiveEditor = false, guiActive = true)]
		public string ConservationMode;

		[KSPEvent(guiActive = false, active = true, guiActiveEditor = true, guiName = "Toggle Bubble Guide", guiActiveUnfocused = false)]
		public void ToggleBubbleGuide()
		{
			var gobj = FindEditorWarpBubble();

			try 
			{
				if (gobj != null) {
					gobj.renderer.enabled = !gobj.renderer.enabled;
					if (gobj.renderer.enabled) {

						var enhancer = FindEnhancer ();
						if (enhancer != null) {
							float enh = 1f + ((float)enhancer.GetEffectiveness (this)) / (float)BubbleSize;
							gobj.transform.localScale = new Vector3 (enh, enh, enh);
						} else {
							gobj.transform.localScale = new Vector3 (1f, 1f, 1f);
						}
					}
				}
			}
			catch (Exception ex)
			{
				print(String.Format("[WSXWARP] Error in ToggleBubbleGuide - {0}", ex.Message));
			}

		}

		public Animation DeployAnimation
		{
			get
			{
				try
				{
					return part.FindModelAnimators(deployAnimationName)[0];
				}
				catch (Exception)
				{
					print("[WSXWARP] ERROR IN GetDeployAnimation");
					return null;
				}
			}
		}
			
		public Animation WarpAnimation
		{
			get
			{
				try
				{
					return part.FindModelAnimators(warpAnimationName)[0];
				}
				catch (Exception)
				{
					print("[WSXWARP] ERROR IN GetWarpAnimation");
					return null;
				}
			}
		}

		private GameObject storedGameObject = null;
		private bool silenceWarnings = false;
		private double currentPlasma;

		private const int LIGHTSPEED = 299792458;
		private const int SUBLIGHT_MULT = 40;
		private const int SUBLIGHT_POWER = 5;
		private const double SUBLIGHT_THROTTLE = .3d;
		private StartState _state;
		private double CurrentSpeed;
		private List<ShipInfo> _shipParts;
		// Angular Momentum Calculation Variables
		private Vector3d TravelDirection;
		private double Speed;
		private CelestialBody PreviousBodyName;
		private double OriginalFrameTrueRadius;
		private double OriginalSpeed;
		private double OriginalMomentumSqr;
		private double SemiLatusOriginal;
		private int ElipMode;
		private float deployTimer = 0f;

		private VInfoBox plasma_gauge;
		private VInfoBox electric_gauge;

		public override void OnStart(StartState state)
		{
			try
			{
				DeployAnimation[deployAnimationName].layer = 3;
				WarpAnimation[warpAnimationName].layer = 4;
				CheckBubbleDeployment(1000);
				base.OnStart(state);
			}
			catch (Exception ex)
			{
				print(String.Format("[WSXWARP] Error in OnStart - {0}", ex.Message));
			}
		}



		public override void OnLoad(ConfigNode node)
		{
			try
			{
				if (_state == StartState.Editor) return;

				silenceWarnings = true;

				// This shouldn't need to activate every time it loads...
				// I think this was to sync it to the engine state, but there
				// is no longer any underlying ModuleEngine to care about
				if (firstActivation) {
					part.force_activate();
					CheckBubbleDeployment(1000);
					firstActivation = false;
				}

				base.OnLoad(node);
				if (AMConservationMode == true)
				{
					ConservationMode = "A.Momentum";
				}
				if (AMConservationMode == false) 
				{
					ConservationMode = "Velocity";
				}

				silenceWarnings = false;

				if (!alertCondition) {
					if (IsActivated) {
						Events ["Activate"].active = false;
						Events ["Shutdown"].active = true;
					} else {
						Events ["Activate"].active = true;
						Events ["Shutdown"].active = false;
					}
				}
			}
			catch (Exception ex)
			{
				print(String.Format("[WSXWARP] Error in OnLoad - {0}", ex.Message));
			}
		}

		private void SetPartState(bool stiffenJoints)
		{
			if (stiffenJoints)
			{
				//Stiffen Joints
				_shipParts = new List<ShipInfo>();
				foreach (var vp in vessel.parts)
				{
					print("[WSXWARP] Stiffening " + vp.name);
					_shipParts.Add(new ShipInfo
						{
							ShipPart = vp,
							BreakingForce = vp.breakingForce,
							BreakingTorque = vp.breakingTorque,
							CrashTolerance = vp.crashTolerance,
							Constraints = vp.rigidbody.constraints
						});
					vp.breakingForce = Mathf.Infinity;
					vp.breakingTorque = Mathf.Infinity;
					vp.crashTolerance = Mathf.Infinity;
				}
				vessel.rigidbody.constraints &= RigidbodyConstraints.FreezeRotation;
			}

			else
			{
				//Stop vessel
				vessel.rigidbody.AddTorque(-vessel.rigidbody.angularVelocity);
				//Reset part state
				if (_shipParts != null)
				{
					foreach (var sp in _shipParts)
					{
						if (vessel.parts.Contains(sp.ShipPart))
						{
							print("[WSXWARP] Relaxing " + sp.ShipPart.name);
							sp.ShipPart.rigidbody.AddTorque(-sp.ShipPart.rigidbody.angularVelocity);
							sp.ShipPart.breakingForce = sp.BreakingForce;
							sp.ShipPart.breakingTorque = sp.BreakingTorque;
							sp.ShipPart.crashTolerance = sp.CrashTolerance;
							sp.ShipPart.rigidbody.constraints = sp.Constraints;
						}
					}
					vessel.rigidbody.constraints &= ~RigidbodyConstraints.FreezeRotation;
				}
			}
		}

		private void WarningSound()
		{
			if (!silenceWarnings) {
				part.Effect (warningEffectName, 1f);
			}
		}

		public void CallRedAlert()
		{
			if (IsActivated) {
				ShutdownDrive(true);
			}
			RedAlert();
		}

		private void RedAlert()
		{
			part.Effect (alertEffectName, 1f);
			SetupAlertEvents();
			alertCondition = true;
			alertTimer = 20.0f;
			status = "Catastrophic Failure!";
		}

		private void SetupAlertEvents()
		{
			Events["CancelRedAlert"].active = true;
			Events["Activate"].active = false;
			Events["Shutdown"].active = false;
		}

		[KSPEvent(guiName = "Cancel Alert", guiActive = true, active = false)]
		private void CancelRedAlert()
		{
			part.Effect (alertEffectName, 0f);
			Events ["CancelRedAlert"].active = false;
			Events ["Activate"].active = true;
			Events ["Shutdown"].active = false;
			alertCondition = false;
		}

		private bool CheckAltitude()
		{
			status = "inactive";
			if (!vessel)
				return false;
			
			if (FlightGlobals.currentMainBody != null) 
			{
				var altCutoff = MinAltitude;
				if (vessel.altitude < altCutoff) {
					status = "Failsafe: " + Math.Round (altCutoff / 1000, 0).ToString() + "km";
					WarningSound ();
					return false;
				}
			}
				
			try
			{
				double sqDisrupt = DisruptRange*DisruptRange;
				var posCur = vessel.GetWorldPos3D();
				foreach (var v in FlightGlobals.Vessels.Where(
					x => x.mainBody == vessel.mainBody))
				{
					var posNext = v.GetWorldPos3D();
					var sqDistance = (posNext-posCur).sqrMagnitude;
					if (sqDistance < sqDisrupt)
					{
						if (v == vessel) continue;
						status = "Failsafe: " + v.vesselName;
						WarningSound ();
						return false;
					}
				}
			}
			catch (Exception ex)
			{
				print (String.Format ("[WSXWARP] - ERROR in CheckAltitude - {0}", ex.Message));
			}


			return true;
		}

		private VInfoBox CreateGauge(string title) {
			VInfoBox vi = part.stackIcon.DisplayInfo();
			vi.SetMsgBgColor(XKCDColors.DarkLime.A(0.6f));
			vi.SetMsgTextColor(XKCDColors.ElectricLime.A(0.6f));
			vi.SetProgressBarColor(XKCDColors.Yellow.A(0.6f));
			vi.SetProgressBarBgColor(XKCDColors.DarkLime.A(0.6f));
			vi.SetLength(2.0f);
			vi.SetMessage(title);
			return vi;
		}

		private void CreateAllGauges() {
			plasma_gauge = CreateGauge ("WarpPlasma");
			electric_gauge = CreateGauge ("ElectricCharge");
		}

		private double UpdateGauge(VInfoBox vi, string resource) {
			double got = 0.0;
			double max = 0.0;
			if (vessel != null) {
				foreach (Part p in vessel.parts) {
					if (p.Resources.Contains (resource)) {
						got += p.Resources[resource].amount;
						max += p.Resources[resource].maxAmount;
					}
				}
			}
			if (max > 0.0) {
				vi.SetValue ((float)(got / max));
			} else {
				vi.SetValue (0.0f);
			}

			return got;

		}

		private bool UpdateAllGauges() {
			try {
				double elec = UpdateGauge (electric_gauge, "ElectricCharge");
				double plas = UpdateGauge (plasma_gauge, "WarpPlasma");

				currentPlasma = plas;

				if (elec <= 1.0 || plas <= 0.0)
					return true;
			}
			catch (Exception ex)
			{
				print(String.Format("[WSXWARP] Error in UpdateAllGauges - {0}", ex.Message));
			}

			return false;
		}

		private void SetPlasmaPowerError(float plasma, float wantPlasma) {
			if (plasma < wantPlasma) {
				status = "No plasma";
			} else {
				status = "No power";
			}
		}


		[KSPEvent(guiName = "Activate Drive", guiActive = true)]
		public void Activate()
		{
			if (!IsActivated && !alertCondition) {
				if (CheckAltitude()) {
					// Don't bother activating if we can't power the drive
					double actpower = 0.0;
					double actplasma = 0.0;
					if (vessel != null) {
						foreach (Part p in vessel.parts) {
							if (p.Resources.Contains("ElectricCharge")) {
								actpower += p.Resources ["ElectricCharge"].amount;
							}
							if (p.Resources.Contains("WarpPlasma")) {
								actplasma += p.Resources ["WarpPlasma"].amount;
							}
						}
					}
					if (actpower < 10f || actplasma < 1f) {
						SetPlasmaPowerError((float)actplasma, 1f);
						WarningSound();
						return;
					}

					part.Effect(engageEffectName, 1.0f);
					IsActivated = true;
					Events["Activate"].active = false;
					Events["Shutdown"].active = true;

					CreateAllGauges();
					UpdateAllGauges();

				}
			}
		}

		[KSPEvent(guiName = "Shutdown Drive", guiActive = true, active = false)]
		public void Shutdown()
		{
			ShutdownDrive(false);
		}

		private void ShutdownDrive(bool flameout)
		{
			if (IsActivated) {
				if (flameout)
					part.Effect(flameoutEffectName, 1.0f);
				else
					part.Effect(disengageEffectName, 1.0f);

				part.Effect(powerEffectName, 0.0f);
				IsActivated = false;
				Events["Shutdown"].active = false;
				Events["Activate"].active = true;

				part.stackIcon.ClearInfoBoxes();

				CurrentSpeed = 0.0;
			}
		}

		[KSPAction("Activate Drive")]
		public void ActivateDriveAction(KSPActionParam ap)
		{
			Activate();
		}

		[KSPAction("Shutdown Drive")]
		public void DeactivateDriveAction(KSPActionParam ap)
		{
			ShutdownDrive(false);
		}

		[KSPAction("Toggle Drive")]
		public void ToggleDriveAction(KSPActionParam ap)
		{
			if (!IsActivated) {
				Activate();
			} else {
				ShutdownDrive(false);
			}
		}

		public override void OnActive()
		{
			Activate();
		}

		private static WarpDriveEnhancer FindEnhancerInPart (Part p)
		{
			var enhancer = p.FindModuleImplementing<WarpDriveEnhancer> ();
			if (enhancer) {
				if (enhancer.AreYouHere ()) {
					return enhancer;
				}
			}
			return null;
		}

		private WarpDriveEnhancer FindEnhancer() 
		{
			try 
			{
				if (vessel != null) {
					foreach (Part p in vessel.parts) {
						WarpDriveEnhancer e = FindEnhancerInPart (p);
						if (e) return e;
					}
				} else {
					// need this for the VAB
					if (part) {
						Part root = part;
						int loops = 0;
						while (root.parent != null) {
							if (root == root.parent) {
								print("[WSXWARP] root == root.parent");
								break;
							}
							root = root.parent;
							loops++;

							if (loops > 3000) {
								print("[WSXWARP] Infinite loop in root search");
								return null;
							}
						}

						Part[] partlist = root.FindChildParts<Part>(true);
						for (var i=0;i<partlist.Length;i++) {
							WarpDriveEnhancer e = FindEnhancerInPart (partlist[i]);
							if (e) return e;
						}
					}
				}
			}
			catch (Exception ex)
			{
				print(String.Format("[WSXWARP] Error in FindEnhancer - {0}", ex.Message));
			}

			return null;
		}

		public void FixedUpdate()
		{
			try
			{
				if (vessel == null || part == null || _state == StartState.Editor) return;

				if (enhancerPoll <= 0) {	
					IsEnhanced = false;
					BubbleEnhancement = 0;
					enhancerPoll = 25;

					var enhancer = FindEnhancer();
					if (enhancer != null) {
						BubbleEnhancement = enhancer.GetEffectiveness(this);
						IsEnhanced = true;
					}

				} else
					enhancerPoll--;

				if (IsDeployed != IsActivated)
				{
					IsDeployed = IsActivated;
					CheckBubbleDeployment(3);
					SetPartState(IsActivated);
				}

				if (!IsDeployed) {
					part.Effect(idleEffectName, 0.0f);
				}

				if (alertCondition) {
					SetupAlertEvents();
					alertTimer -= TimeWarp.fixedDeltaTime;
					if (alertTimer <= 0.0) {
						CancelRedAlert();
					}
					return;
				}
					
				if (IsDeployed)
				{
					float currentThrottle = vessel.ctrlState.mainThrottle * (displayWarpFactor/10.0f);

					//Failsafe
					if (deployTimer <= 0.0) {
						// The Kraken tends to eat things that get close to the bubble
						if (currentThrottle < MinThrottle) {
							if (!CheckBubbleDistances()) {
								return;
							}
						}

						if (!CheckAltitude()) {
							ShutdownDrive(false);
							return;
						}
					}

					if (IsActivated && plasma_gauge == null) CreateAllGauges();

					double dt = (double)TimeWarp.fixedDeltaTime;

					if (deployTimer > 0.0) {
						deployTimer -= (float)dt;
						PlayWarpAnimation(currentThrottle);
						UpdateAllGauges();
						return;
					}


					part.Effect(idleEffectName, 1.0f);
					part.Effect(powerEffectName, currentThrottle);

					bool flameout = false;

					if (currentThrottle >= MinThrottle) {
						// Use up Plasma and Electricity to maintain the warp field
						float plasmaNeeded = (float)(PlasmaMultiply * WarpFactor * currentThrottle * dt);
						float elecNeeded = (float)(ElectricMultiply * WarpFactor * currentThrottle * dt);

						float plasmaUsed = part.RequestResource("WarpPlasma", plasmaNeeded);
						float elecUsed = part.RequestResource("ElectricCharge", elecNeeded);
						if (plasmaUsed < plasmaNeeded || elecUsed < elecNeeded) {
							flameout = true;
							SetPlasmaPowerError(plasmaUsed, plasmaNeeded);
						}
					}	
					if (UpdateAllGauges()) {
						flameout = true;
						SetPlasmaPowerError((float)currentPlasma, 1.0f);
					}

					//OH NO FLAMEOUT!
					// (actually it isn't all that bad anymore)
					if (flameout)
					{
						WarningSound ();
						BubbleCollapse();
						ShutdownDrive(true);
						return;
					}

					PlayWarpAnimation(currentThrottle);

					if (currentThrottle < MinThrottle)
						return;

					//Start by adding in our subluminal speed which is exponential
					double lowerThrottle = (Math.Min(currentThrottle, SUBLIGHT_THROTTLE) * SUBLIGHT_MULT);
					double distance = Math.Pow(lowerThrottle, SUBLIGHT_POWER);

					//Then if throttle is over our threshold, go linear
					if (currentThrottle > SUBLIGHT_THROTTLE)
					{
						//How much headroom do we have
						double maxSpeed = (LIGHTSPEED/50*WarpFactor) - distance;
						//How much of this can we use?
						var upperThrottle = currentThrottle - SUBLIGHT_THROTTLE;
						//How much of this headroom have we used?
						var throttlePercent = upperThrottle/(1 - SUBLIGHT_THROTTLE);
						//Add it to our current throttle calculation
						var additionalDistance = maxSpeed*throttlePercent;
						distance += additionalDistance;
					}


					//Take into acount safe accelleration/decelleration
					if (distance > CurrentSpeed + Math.Pow(10,MaxAccelleration))
						distance = CurrentSpeed + Math.Pow(10, MaxAccelleration);
					if (distance < CurrentSpeed - Math.Pow(10, MaxAccelleration))
						distance = CurrentSpeed - Math.Pow(10, MaxAccelleration);
					CurrentSpeed = distance;

					if (distance > 1000)
					{
						//Let's see if we can get rid of precision issues with distance.
						Int32 precision = Math.Round(distance, 0).ToString().Length - 1;
						if (precision > MaxAccelleration) precision = MaxAccelleration;
						var magnitude = Math.Round((distance / Math.Pow(10, precision)),0);
						var jumpDistance = Math.Pow(10,precision) * magnitude;
						distance = jumpDistance;
					}


					double c = (distance * 50) / LIGHTSPEED;
					status = String.Format("{1:n0} m/s [{0:0}%c]", c*100f, distance * 50);
					if (currentThrottle >= MinThrottle)
					{
						// Translate through space on the back of a Kraken!
						Vector3d ps = vessel.transform.position + (transform.up*(float)(distance));
						Krakensbane krakensbane = (Krakensbane)FindObjectOfType(typeof(Krakensbane));
						krakensbane.setOffset(ps);
						//AngularMomentum Block
						if (AMConservationMode == true)
						{
							ApplyAngularMomentum();
						}
					}
					if (AMConservationMode == true && currentThrottle == 0)
					{
						SetAMStartStateVars();
					}
				}
			}
			catch (Exception ex)
			{
				print(String.Format("[WSXWARP] Error in OnFixedUpdate - {0}", ex.Message));
			}
		}

		private void ApplyAngularMomentum()
		{
			if (PreviousBodyName == FlightGlobals.currentMainBody)
			{
				if ((FlightGlobals.ActiveVessel.orbit.eccentricity > 1) && (ElipMode == 0)) //For Hyperbolic Orbits. Conserve angular momentum by making orbit.h constant. GMp=h^2, so semi-latus rectum must be constant as well.).
				{
					Speed = Math.Sqrt(FlightGlobals.ActiveVessel.mainBody.gravParameter*((2/FlightGlobals.ActiveVessel.orbit.radius)-((SemiLatusOriginal*FlightGlobals.ActiveVessel.mainBody.gravParameter)/(FlightGlobals.ActiveVessel.orbit.semiMajorAxis * OriginalMomentumSqr))));
					if (Vector3d.Magnitude (Krakensbane.GetFrameVelocity ()) > 0) 
					{
						var VelocityOffset = (TravelDirection * Speed) - Krakensbane.GetFrameVelocity ();
						FlightGlobals.ActiveVessel.ChangeWorldVelocity (VelocityOffset);
					}
					else 
					{
						var VelocityOffset = (TravelDirection * Speed);
						FlightGlobals.ActiveVessel.SetWorldVelocity (VelocityOffset);
					}
				}
				if ((FlightGlobals.ActiveVessel.orbit.eccentricity <= 1) && (ElipMode == 1)) // For Elliptical Orbits. Conserve Angular Momentum directly by altering state vectors
				{
					Speed = OriginalSpeed * (OriginalFrameTrueRadius / (FlightGlobals.ActiveVessel.orbit.radius));
					if (Vector3d.Magnitude (Krakensbane.GetFrameVelocity ()) > 0) 
					{
						var VelocityOffset = (TravelDirection * Speed) - Krakensbane.GetFrameVelocity ();
						FlightGlobals.ActiveVessel.ChangeWorldVelocity (VelocityOffset);
					}
					if (Vector3d.Magnitude (Krakensbane.GetFrameVelocity ()) == 0) 
					{
						var VelocityOffset = (TravelDirection * Speed);
						FlightGlobals.ActiveVessel.SetWorldVelocity (VelocityOffset);
					}
					if (((OriginalFrameTrueRadius / FlightGlobals.ActiveVessel.orbit.radius) <= 0.55) || ((OriginalFrameTrueRadius / FlightGlobals.ActiveVessel.orbit.radius) <= 1.75)) // re-set variables when ratio between current ratio and original gets too far from 1
					{
						OriginalSpeed = Vector3d.Magnitude (FlightGlobals.ActiveVessel.orbit.GetRelativeVel ());
						OriginalFrameTrueRadius = FlightGlobals.ActiveVessel.orbit.radius;
					}
				}
			}
			if (((FlightGlobals.ActiveVessel.orbit.eccentricity < 1) && (ElipMode == 0)) || ((FlightGlobals.ActiveVessel.orbit.eccentricity > 1) && (ElipMode == 1)) || (PreviousBodyName != FlightGlobals.currentMainBody))
			if (PreviousBodyName != FlightGlobals.currentMainBody)
			{
				SetAMStartStateVars();
			}

		}

		private void SetAMStartStateVars()
		{
			TravelDirection = Vector3d.Normalize(FlightGlobals.ActiveVessel.orbit.GetRelativeVel());
			OriginalSpeed = Vector3d.Magnitude(FlightGlobals.ActiveVessel.orbit.GetRelativeVel());
			OriginalFrameTrueRadius = FlightGlobals.ActiveVessel.orbit.radius;
			OriginalMomentumSqr = Vector3d.SqrMagnitude(FlightGlobals.ActiveVessel.orbit.h.xzy);
			PreviousBodyName = FlightGlobals.currentMainBody;
			SemiLatusOriginal = FlightGlobals.ActiveVessel.orbit.semiLatusRectum;
			if (FlightGlobals.ActiveVessel.orbit.eccentricity > 1)
			{
				ElipMode = 0;
			}
			else
			{
				ElipMode = 1;
			}
		}
			
		private void BubbleCollapse()
		{
			FlightInputHandler.state.mainThrottle = 0;	
			IsDeployed = false;
			CheckBubbleDeployment (3);
			SetPartState (false);
		}

		private void PlayWarpAnimation(float throttle)
		{
			try
			{
				float light = 0f;
				if (BubbleEnhancement > 0) light = 1f;

				WarpAnimation[warpAnimationName].speed = 0.5f + (throttle * 4.0f) + 0.5f*light;
				if (!WarpAnimation.IsPlaying(warpAnimationName))
				{
					WarpAnimation.Play(warpAnimationName);
				}
				//Set our color
				if (storedGameObject == null) {
					MeshRenderer[] pRenderers = part.FindModelComponents<MeshRenderer>();
					for (var i=0;i<pRenderers.Length;i++) {
						MeshRenderer mr = pRenderers[i];
						if (mr.gameObject.tag == "Icon_Hidden") {
							if (mr.gameObject.name == "Torus_001") {
								storedGameObject = mr.gameObject;
								break;
							}
						}
					}
				}
					
				var c = new Color(0.2f + 0.2f*throttle + 0.2f*light, 0.3f + 0.5f*throttle, 0.3f + 0.6f*throttle + 0.1f*light);
				storedGameObject.renderer.material.SetColor("_Color", c);

				// Animated bubble ends up bigger?
				float enh = tweakFactor * (1f + ((float)BubbleEnhancement / (float)BubbleSize));
				storedGameObject.renderer.transform.localScale = new Vector3 (enh, enh, enh);

			}
			catch (Exception)
			{
				print("[WSXWARP] ERROR IN PlayWarpAnimation");
			}
		}

		//[KSPEvent(guiName = "Debug", guiActive = true)]
		public void DebugBubble()
		{
			CheckBubbleDistances ();
		}

		private bool CheckBubbleDistances()
		{
			try
			{
				int partn = 0;
				Part[] ExplodeParts = new Part[10];
				bool SomethingExploded = false;

				var posBubble = part.partTransform.position;
				float sqrBubbleSize = BubbleSize+BubbleEnhancement;
				sqrBubbleSize *= sqrBubbleSize;
				bool inside, outside;
				int ex;

				foreach (var p in vessel.parts)
				{
					if (p == part) continue;

					if (p.physicalSignificance == Part.PhysicalSignificance.NONE)
						continue;

					float longest = -1;

					inside = false;
					outside = false;
					ex = 1;

					bool ignoreThis = false;;

					// The Communotron 99-99 causes problems
					if (p.FindModuleImplementing<ModuleDataTransmitter>())
						ignoreThis = true;

					if (ignoreThis)
						continue;

					MeshFilter[] mf = p.FindModelComponents<MeshFilter>();
					for (var i=0;i<mf.Length;i++) {
						Bounds mrb = mf[i].mesh.bounds;

						for (var z=-1;z<=1;z+=2) {
							for (var y=-1;y<=1;y+=2) {
								for (var x=-1;x<=1;x+=2) {
									// xzy because that's the coordinate system KSP likes to use
									Vector3 boxpt = new Vector3(mrb.center.x + ex * x * mrb.extents.x,
										mrb.center.z + ex * z * mrb.extents.z, mrb.center.y + ex * y * mrb.extents.y);
									Vector3 tpt = p.transform.TransformPoint(boxpt);

									float sqrDistance = (tpt-posBubble).sqrMagnitude;
									if (sqrDistance <= sqrBubbleSize) {
										inside = true;
									} else {
										outside = true;
									}

									if (sqrDistance > longest) {
										longest = sqrDistance;
									}
										
									// The bubble cuts this one in half
									if (inside && outside) {
										SomethingExploded = true;
										ExplodeParts[partn++] = p;
										print("[WSXWARP] Bubble hit " + p.name + " at dist " + Math.Sqrt(longest));
										if (partn == ExplodeParts.Length) {
											Array.Resize<Part>(ref ExplodeParts, ExplodeParts.Length*2);
										}
										goto GotoConsideredHarmful;
									}

									if (ex == 0)
										goto GotoConsideredHarmful;
								}
							}
						}
					}

					GotoConsideredHarmful:
					continue;
				}

				if (SomethingExploded) {					
					BubbleCollapse();
					ShutdownDrive(true);

					for (var x=0;x<partn;x++) {
						WSXStuff.PowerfulExplosion(ExplodeParts[x]);
					}

					WarningSound();
					RedAlert();
					return false;
				}
			}
			catch (Exception ex)
			{
				print(String.Format("[WSXWARP] Error in CheckBubbleDistances - {0}", ex.Message));
			}

			return true;
		}


		private void CheckBubbleDeployment(int wantspeed)
		{
			try
			{
				int speed = wantspeed;
				if (BubbleEnhancement > 0 && speed < 1000)
					speed = wantspeed*5;
					
				//print("[WSXWARP] CHECKING BUBBLE " + speed);
				//Turn off guide if there              
				if (IsDeployed)
				{
					SetDeployedState(speed);
				}
				else
				{
					SetRetractedState(-speed);
				}
				if (_state != StartState.Editor)
				{
					GameObject gobj = FindEditorWarpBubble();
					if (gobj != null)
						gobj.renderer.enabled = false;
				}
			}
			catch (Exception)
			{
				print("[WSXWARP] ERROR IN CheckBubbleDeployment");
			}
		}
		private GameObject FindEditorWarpBubble()
		{
			
			foreach (var gobj in GameObject.FindObjectsOfType<GameObject>())
			{
				if (gobj.name == "EditorWarpBubble" && gobj.renderer != null)
					return gobj;
			}

			return null;
		}
		private void SetRetractedState(int speed)
		{
			try
			{
				IsDeployed = false;
				PlayDeployAnimation(speed);
			}
			catch (Exception)
			{
				print("[WSXWARP] ERROR IN SetRetractedState");
			}
		}
		private void SetDeployedState(int speed)
		{
			try
			{
				IsDeployed = true;
				PlayDeployAnimation(speed);

				deployTimer = 1f;
				if (BubbleEnhancement > 0)
					deployTimer = 0.5f;
			
				//For Angular Momentum Calculations
				if (AMConservationMode == true)
				{
					SetAMStartStateVars();
				}
			}
			catch (Exception)
			{
				print("[WSXWARP] ERROR IN SetDeployedState");
			}
		}

		private void PlayDeployAnimation(int speed)
		{
			try
			{
				if (speed < 0)
				{
					DeployAnimation[deployAnimationName].time = DeployAnimation[deployAnimationName].length;
				}
				DeployAnimation[deployAnimationName].speed = speed;
				DeployAnimation.Play(deployAnimationName);
			}
			catch (Exception)
			{
				print("[WSXWARP] ERROR IN PlayDeployAnimation");
			}
		}
	}
}

