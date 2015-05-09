using System;
using UnityEngine;

namespace WarpShip
{
	public class ContainmentSystem : PartModule
	{
		[KSPField(isPersistant = false)]
		public string resourceHeld = "WarpPlasma";

		[KSPField(isPersistant = false)]
		public string resourceUsed = "ElectricCharge";

		[KSPField(isPersistant = false)]
		public double ratio = 0.05;

		[KSPField(isPersistant = false)]
		public bool stable = false;

		double ChargeNeeded(Part part)
		{
			double charge = 0.0;
			if (part != null) {
				if (part.Resources.Contains (resourceHeld)) {
					charge += (part.Resources [resourceHeld].amount * ratio); 
				}
			}
			return charge;
		}

		double ChargeAvailable()
		{
			return WSXStuff.ThingAvailable (vessel, resourceUsed);
		}
			
		public bool ContainmentBreach()
		{
			WSXStuff.RedAlert(vessel);
			WSXStuff.PowerfulExplosion(part);
			return false;
		}
			
		public override string GetInfo()
		{
			if (stable) {
				return "<b>Self-Powered</b>";
			} else {
				double cn = ChargeNeeded (part);
				return String.Format ("<b>Peak {0} Used:</b> {1:F2}/sec.", resourceUsed, cn);
			}
		}

		public void FixedUpdate()
		{
			if (stable || vessel == null)
				return;
			
			double dt = (double)TimeWarp.fixedDeltaTime;
			double c_needed = ChargeNeeded (part);
			double needed = c_needed * dt;
			double avail = ChargeAvailable ();

			if (avail < needed) {
				part.temperature += 1600.0 * dt;
				if (part.temperature > part.maxTemp) {
					ContainmentBreach ();
				}
				return;
			}
			part.RequestResource (resourceUsed, needed);

		}
	}
}

