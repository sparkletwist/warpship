using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WarpShip
{
	public class Develocitizer : PartModule
	{
		[KSPField(isPersistant = false)]
		public string engageEffectName = "engage";

		[KSPField(isPersistant = false)]
		public float powerMultiplier = 8.0f;

		[KSPField(guiActive = true, guiName = "Develocitizer", guiActiveEditor = false)]
		public string rechargeNotice = "Ready";

		[KSPField(isPersistant = true, guiActive = true, guiName = "Reference Frame", guiActiveEditor = false)]
		public string rFrame = "Local";

		[KSPField(isPersistant = true)]
		public float rFrameVal = 0;

		[KSPField(isPersistant = true)]
		public float recharge = 0.0f;

		[KSPField(isPersistant = true)]
		public float powerDrain = 0.0f;

		[KSPField(isPersistant = true)]
		public float maxPowerDrain = 0.0f;

		[KSPField(isPersistant = false)]
		public string resourceUsed = "ElectricCharge";

		public override string GetInfo()
		{
			return String.Format ("<b>{0} Used:</b> {1:F2} per m/sec.", resourceUsed, powerMultiplier);
		}

		[KSPEvent(guiName = "Change Ref. Frame", guiActive = true)]
		public void ChangeRefFrame()
		{
			rFrameVal++;
			if (rFrame == "Local") {
				rFrame = "Planetary";
			} else if (rFrame == "Planetary") {
				rFrame = "System";
			} else if (rFrame == "System") {
				rFrame = "Local";
				rFrameVal = 0;
			}
		}

		[KSPEvent(guiName = "Develocitize", guiActive = true)]
		public void Develocitize()
		{
			// This code is inspired quite heavily by HyperEdit's OrbitEditor.cs
			if (recharge <= 0.0f) {
				double ut = Planetarium.GetUniversalTime ();
				Orbit curo = vessel.orbitDriver.orbit;
				Vector3d currentVelocity = curo.getOrbitalVelocityAtUT (ut);
				Vector3d prograde = currentVelocity.normalized;

				bool nomoon = false;
				Vector3d vlocal = new Vector3d(0.0, 0.0, 0.0);
				Vector3d vplanetary = new Vector3d(0.0, 0.0, 0.0);
				if (vessel.mainBody && vessel.mainBody.orbitDriver) {
					vlocal = vessel.mainBody.orbitDriver.orbit.getOrbitalVelocityAtUT (ut);

					CelestialBody rb = vessel.mainBody.referenceBody;
					if (rb != null && rb != vessel.mainBody && rb.orbitDriver) {
						vplanetary = rb.orbitDriver.orbit.getOrbitalVelocityAtUT (ut);
					} else {
						vplanetary = vlocal;
						vlocal = new Vector3d (0.0, 0.0, 0.0);
						nomoon = true;
					}
				}	

				// Do not call GetObtVelocity and expect it to work with this code
				// It switches around Y and Z
				Vector3d velocityToCancel = new Vector3d(0.0, 0.0, 0.0);
				if (rFrameVal > 0)
					velocityToCancel += vlocal;
				if (rFrameVal > 1)
					velocityToCancel += vplanetary;
				Vector3d exVelocityToCancel = velocityToCancel;
				velocityToCancel += currentVelocity;

				double speedToCancel = velocityToCancel.magnitude;
				double powerCost = (int)speedToCancel * powerMultiplier;
				double powerGot = WSXStuff.ThingAvailable(vessel, resourceUsed);
				if (powerGot < powerCost) {
					rechargeNotice = "No power";
					this.recharge = 2.0f;
					return;
				}

				powerDrain = (float)powerCost;
				maxPowerDrain = powerDrain;
				part.Effect(engageEffectName, 1.0f);

				// Extremely small velocities cause the game to mess up very badly, so try something small and increase...
				float mult = 0.0f;
				if (rFrameVal == 0 || nomoon)
					mult = 2.0f;
				Orbit newo;
				do {
					Vector3d retro = prograde * -mult;
					newo = new Orbit (curo.inclination, curo.eccentricity, curo.semiMajorAxis,
					             curo.LAN, curo.argumentOfPeriapsis, curo.meanAnomalyAtEpoch, curo.epoch, curo.referenceBody);
					newo.UpdateFromStateVectors (curo.pos, retro - exVelocityToCancel, curo.referenceBody, ut);
					mult += 1.0f;
				} while (double.IsNaN(newo.getOrbitalVelocityAtUT (ut).magnitude));

				mult -= 1.0f;
				print ("[WSXDV] Needed Multiplier " + mult.ToString());

				vessel.Landed = false;
				vessel.Splashed = false;
				vessel.landedAt = string.Empty;

				// I'm actually not sure what this is for... but HyperEdit does it.
				// I had weird problems when I took it out, anyway.
				try
				{
					OrbitPhysicsManager.HoldVesselUnpack(60);
				}
				catch (NullReferenceException)
				{
					print("[WSXDV] NullReferenceException");
				}
				var allVessels = FlightGlobals.fetch == null ? (IEnumerable<Vessel>)new[] { vessel } : FlightGlobals.Vessels;
				foreach (var v in allVessels.Where(v => v.packed == false))
					v.GoOnRails();
				// End HyperEdit code I don't really understand

				curo.inclination = newo.inclination;
				curo.eccentricity = newo.eccentricity;
				curo.semiMajorAxis = newo.semiMajorAxis;
				curo.LAN = newo.LAN;
				curo.argumentOfPeriapsis = newo.argumentOfPeriapsis;
				curo.meanAnomalyAtEpoch = newo.meanAnomalyAtEpoch;
				curo.epoch = newo.epoch;
				curo.Init();
				curo.UpdateFromUT(ut);

				vessel.orbitDriver.pos = vessel.orbit.pos.xzy;
				vessel.orbitDriver.vel = vessel.orbit.vel;

				Events ["Develocitize"].active = false;
				this.recharge = 5.0f;
			}
		}

		[KSPAction("Change Ref Frame")]
		public void ChangeRefFrameAction(KSPActionParam ap)
		{
			ChangeRefFrame();
		}

		[KSPAction("Develocitize")]
		public void DevelocitizeAction(KSPActionParam ap)
		{
			Develocitize();
		}

		public void FixedUpdate() 
		{
			if (powerDrain > 0.0f || recharge > 0.0f) {
				Events ["Develocitize"].active = false;
				float dt = TimeWarp.fixedDeltaTime;
				recharge -= dt;

				if (powerDrain > 0.0f) {
					rechargeNotice = "Charging...";
					float curPowerDrain = (maxPowerDrain / 5.0f) * dt;
					if (powerDrain < curPowerDrain)
						curPowerDrain = powerDrain;					
					curPowerDrain = part.RequestResource ("ElectricCharge", curPowerDrain);
					powerDrain -= curPowerDrain;
				}
				
				if (recharge <= 0.0f) {
					if (powerDrain > 0.0f) {
						recharge = 0.1f;
						return;
					}
					recharge = 0.0f;
					Events ["Develocitize"].active = true;
					rechargeNotice = "Ready";
				}
			}
		}
	}
}

