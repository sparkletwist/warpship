using System;
using UnityEngine;

namespace WarpShip
{
	public class WSXStuff
	{
		public static double ThingAvailable(Vessel vessel, string resource)
		{
			double charge = 0.0;
			if (vessel != null) {
				foreach (Part p in vessel.parts) {
					if (p.Resources.Contains (resource)) {
						charge += p.Resources [resource].amount;
					}
				}
			}
			return charge;
		}
	}

	public class ContainmentSystem : PartModule
	{
		[KSPField(isPersistant = false)]
		string resourceHeld = "WarpPlasma";

		[KSPField(isPersistant = false)]
		string resourceUsed = "ElectricCharge";

		[KSPField(isPersistant = false)]
		double ratio = 0.1;

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
			
		public override string GetInfo()
		{
			double rounded_cn = (int)(ChargeNeeded (part) * 1000);
			rounded_cn /= 1000;
			return String.Format ("<b>{0} Used:</b> {1:F}/sec.", resourceUsed, rounded_cn);
		}

		public void FixedUpdate()
		{
			double dt = (double)TimeWarp.fixedDeltaTime;
			double c_needed = ChargeNeeded (part);
			double needed = c_needed * dt;
			double avail = ChargeAvailable ();

			if (avail < needed) {
				Part[] ship_parts = vessel.Parts.ToArray ();
				foreach (Part p in ship_parts) {
					if (p != vessel.rootPart && p != this.part) {
						p.explode ();
					}
				}
				vessel.rootPart.explode ();
				this.part.explode ();
				return;
			}
			part.RequestResource (resourceUsed, needed);

		}
	}
}

