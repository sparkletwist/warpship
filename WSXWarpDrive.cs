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
		public string spoolEffectName = "spool";
		[KSPField]
		public string powerEffectName = "power";
		[KSPField]
		public string flameoutEffectName = "flameout";

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Warp Factor") , UI_FloatRange(minValue = 0f, maxValue = 10f, stepIncrement = 0.5f)]
		public float displayWarpFactor = 10f;

		[KSPField(guiActive = true, guiName = "Warp Drive", guiActiveEditor = false)]
		public string status = "inactive";

		[KSPField]
		public string deployAnimationName = "Engage";

		[KSPField]
		public string warpAnimationName = "WarpField";

		[KSPField] 
		public float WarpFactor = 1.65f;

		[KSPField]
		public float Demasting = 10f;

		[KSPField]
		public int MaxAccelleration = 4;

		[KSPField]
		public float MinThrottle = 0.05f;

		[KSPField(isPersistant = true)] 
		public bool IsDeployed = false;

		[KSPField(isPersistant = true)] 
		public bool IsActivated = false;

		[KSPField]
		public double DisruptRange = 2000.0;

		[KSPField]
		public int BubbleSize = 20;

		[KSPField]
		public float MinAltitude = 1f;

		[KSPField(guiName = "Conservation", isPersistant = true, guiActiveEditor = true, guiActive = false)]
		[UI_Toggle(disabledText = "Velocity", enabledText = "Angular Momentum")]
		protected bool AMConservationMode = false;
		[KSPField(guiName = "Conservation", guiActiveEditor = false, guiActive = true)]
		public string ConservationMode;

		[KSPEvent(guiActive = false, active = true, guiActiveEditor = true, guiName = "Toggle Bubble Guide", guiActiveUnfocused = false)]
		public void ToggleBubbleGuide()
		{
			var gobj = FindEditorWarpBubble();
			if (gobj != null)
				gobj.renderer.enabled = !gobj.renderer.enabled;

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

				part.force_activate();
				CheckBubbleDeployment(1000);

				base.OnLoad(node);
				if (AMConservationMode == true)
				{
					ConservationMode = "A.Momentum";
				}
				if (AMConservationMode == false) 
				{
					ConservationMode = "Velocity";
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

		private bool CheckAltitude()
		{
			status = "inactive";
			if (FlightGlobals.currentMainBody != null) 
			{
				var altCutoff = FlightGlobals.currentMainBody.Radius * MinAltitude;
				if (vessel.altitude < altCutoff) {
					status = "failsafe: " + Math.Round (altCutoff / 1000, 0) + "km";
					return false;
				}
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

		private void UpdateGauge(VInfoBox vi, string resource) {
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
			
		}

		private void UpdateAllGauges() {
			UpdateGauge (electric_gauge, "ElectricCharge");
			UpdateGauge (plasma_gauge, "WarpPlasma");
		}

		private void SetPlasmaPowerError(float plasma, float wantPlasma) {
			if (plasma < wantPlasma) {
				status = "no plasma";
			} else {
				status = "no power";
			}
		}


		[KSPEvent(guiName = "Activate Drive", guiActive = true)]
		public void Activate()
		{
			if (!IsActivated) {
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
			if (IsActivated) {
				part.Effect(disengageEffectName, 1.0f);
				IsActivated = false;
				Events["Shutdown"].active = false;
				Events["Activate"].active = true;

				part.stackIcon.ClearInfoBoxes();
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
			Shutdown();
		}

		[KSPAction("Toggle Drive")]
		public void ToggleDriveAction(KSPActionParam ap)
		{
			if (!IsActivated) {
				Activate();
			} else {
				Shutdown();
			}
		}

		public override void OnActive()
		{
			Activate();
		}

		public void FixedUpdate()
		{
			try
			{
				if (vessel == null || part == null || _state == StartState.Editor) return;

				if (IsDeployed != IsActivated)
				{
					IsDeployed = IsActivated;
					CheckBubbleDeployment(3);
					SetPartState(IsActivated);
				}

				if (IsDeployed)
				{
					//Failsafe
					if (!CheckAltitude())
					{
						Shutdown();
						return;
					}

					//Snip partsx
					DecoupleBubbleParts();
					//Other ships -isn't working for some reason,  throwing errors to log
					DestroyNearbyShips(DisruptRange, false);

					if (IsActivated && plasma_gauge == null) CreateAllGauges();

					double dt = (double)TimeWarp.fixedDeltaTime;
					bool flameout = false;
		
					float currentThrottle = vessel.ctrlState.mainThrottle * (displayWarpFactor/10.0f);
					if (currentThrottle > MinThrottle) {
						// Use up Plasma and Electricity to maintain the warp field
						float plasmaNeeded = (float)(10.0 * currentThrottle * dt);
						float elecNeeded = (float)(100.0 * currentThrottle * dt);

						float plasmaUsed = part.RequestResource("WarpPlasma", plasmaNeeded);
						float elecUsed = part.RequestResource("ElectricCharge", elecNeeded);
						if (plasmaUsed < plasmaNeeded || elecUsed < elecNeeded) {
							flameout = true;
							SetPlasmaPowerError(plasmaUsed, plasmaNeeded);
						}
					} else {
						currentThrottle = 0f;
					}
					UpdateAllGauges();

					//OH NO FLAMEOUT!
					if (flameout)
					{
						FlightInputHandler.state.mainThrottle = 0;
						BubbleCollapse();
						Shutdown();
						return;
					}

					PlayWarpAnimation(currentThrottle);

					//Start by adding in our subluminal speed which is exponential
					double lowerThrottle = (Math.Min(currentThrottle, SUBLIGHT_THROTTLE) * SUBLIGHT_MULT);
					double distance = Math.Pow(lowerThrottle, SUBLIGHT_POWER);

					//Then if throttle is over our threshold, go linear
					if (currentThrottle > SUBLIGHT_THROTTLE)
					{
						//How much headroon do we have
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
					if (currentThrottle > MinThrottle)
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
			IsDeployed = false;
			CheckBubbleDeployment(3);
			SetPartState(false);
		}

		private void PlayWarpAnimation(float speed)
		{
			try
			{
				WarpAnimation[warpAnimationName].speed = 0.5f + (speed * 2.0f);
				if (!WarpAnimation.IsPlaying(warpAnimationName))
				{
					WarpAnimation.Play(warpAnimationName);
				}
				//Set our color
				foreach (var gobj in GameObject.FindGameObjectsWithTag("Icon_Hidden"))
				{
					if (gobj.name == "Torus_001")
					{
						//var rgb = ColorUtils.HSL2RGB(Math.Abs(speed - 1), 0.5, speed / 2);
						var c = new Color(0.2f, 0.3f, 0.3f);
						gobj.renderer.material.SetColor("_Color", c);
						//gobj.transform.localScale = new Vector3(1.0f + speed, 1.0f + speed, 1.0f + speed);
					}
				}
			}
			catch (Exception)
			{
				print("[WSXWARP] ERROR IN PlayWarpAnimation");
			}
		}
		private void DecoupleBubbleParts()
		{
			try
			{
				foreach (var p in vessel.parts)
				{
					var posPart = p.partTransform.position;
					var posBubble = part.partTransform.position;
					double distance = Vector3d.Distance(posBubble, posPart);
					if (distance > BubbleSize)
					{
						print("[WARP] Decoupling Part " + p.name);
						p.decouple();
					}
				}
			}
			catch (Exception)
			{
				print("[WSXWARP] ERROR IN DecoupleBubbleParts");
			}
		}
		private void CheckBubbleDeployment(int speed)
		{
			try
			{
				//print("[WSXWARP] CHECKING BUBBLE " + speed);
				//Turn off guide if there              
				if (IsDeployed)
				{
					SetDeployedState(speed);
				}
				else
				{
					SetRetractedState(-speed);
					CheckAltitude();
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



		private void DestroyNearbyShips(double within, bool includeSelf)
		{
			try
			{
				var ships = GetNearbyVessels(within, includeSelf);
				foreach (var s in ships)
				{
					foreach (var p in s.parts)
					{
						p.explode();
					}
				}
			}
			catch (Exception)
			{
				print("[WSXWARP] ERROR IN DestroyNearbyShips");
			}
		}

		private List<Vessel> GetNearbyVessels(double range, bool includeSelf)
		{
			try
			{
				var posCur = vessel.GetWorldPos3D();
				var vessels = new List<Vessel>();
				foreach (var v in FlightGlobals.Vessels.Where(
					x => x.mainBody == vessel.mainBody))
				{
					if (v == vessel && !includeSelf) continue;
					var posNext = v.GetWorldPos3D();
					var distance = Vector3d.Distance(posCur, posNext);
					if (distance < range)
					{
						vessels.Add(v);
					}
				}
				return vessels;
			}
			catch (Exception ex)
			{
				print(String.Format("[WSXWARP] - ERROR in GetNearbyVessels - {0}", ex.Message));
				return new List<Vessel>();
			}
		}
	}
}

