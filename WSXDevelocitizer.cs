using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WarpShip
{
	public class Develocitizer : PartModule
	{
		[KSPField]
		public string engageEffectName = "engage";

		[KSPField(guiActive = true, guiName = "Develocitizer", guiActiveEditor = false)]
		public string rechargeNotice = "Ready";

		[KSPField(isPersistant = true)]
		public float recharge = 0.0f;

		[KSPField(isPersistant = true)]
		public float powerDrain = 0.0f;

		[KSPField(isPersistant = true)]
		public float maxPowerDrain = 0.0f;

		[KSPEvent(guiName = "Develocitize", guiActive = true)]
		public void Develocitize()
		{
			// This code is inspired quite heavily by HyperEdit's OrbitEditor.cs
			if (recharge <= 0.0f) {
				double ut = Planetarium.GetUniversalTime ();
				Orbit curo = vessel.orbitDriver.orbit;
				Vector3d prograde = curo.getOrbitalVelocityAtUT(ut).normalized;

				float obt_mag = vessel.GetObtVelocity().magnitude;
				double powerCost = (int)obt_mag / 10;
				double powerGot = WSXStuff.ThingAvailable(vessel, "ElectricCharge");

				if (powerGot < powerCost) {
					rechargeNotice = "no power";
					this.recharge = 2.0f;
					return;
				}

				powerDrain = (float)powerCost;
				maxPowerDrain = powerDrain;
				part.Effect(engageEffectName, 1.0f);
				
				// Extremely small velocities cause the game to mess up very badly, so try something small and increase...
				float mult = 2.0f;
				Orbit newo;
				do {
					Vector3d tiny_prograde = prograde * mult;
					newo = new Orbit (curo.inclination, curo.eccentricity, curo.semiMajorAxis,
					             curo.LAN, curo.argumentOfPeriapsis, curo.meanAnomalyAtEpoch, curo.epoch, curo.referenceBody);
					newo.UpdateFromStateVectors (curo.pos, tiny_prograde, curo.referenceBody, ut);
					mult += 2.0f;
				} while (double.IsNaN(newo.getOrbitalVelocityAtUT (ut).magnitude));

				mult -= 2.0f;
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
				rechargeNotice = "Charging...";
				this.recharge = 5.0f;
			}
		}

		[KSPAction("Develocitize")]
		public void DevelocitizeAction(KSPActionParam ap)
		{
			Develocitize();
		}

		public void FixedUpdate() {
			if (recharge > 0.0f) {
				float dt = TimeWarp.fixedDeltaTime;
				recharge -= dt;

				if (powerDrain > 0.0f) {
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

