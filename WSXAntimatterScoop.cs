using System;

namespace WarpShip
{
	// Inspired by Interstellar, although my algorithm for antimatter availability is different
	public class AntimatterScoop : PartModule
	{

		[KSPField(guiActive = true, guiName = "Flow", guiActiveEditor = false)]
		public string showInflow = "?";

		[KSPField(isPersistant = true)]
		public bool IsActive = false;

		[KSPField(isPersistant = false)]
		public string processInto = "WarpPlasma";

		[KSPField(isPersistant = false)]
		public float processRatio = 1.0f;

		[KSPField(isPersistant = false)]
		public float scaleFactor = 2.3148148f;

		[KSPField(isPersistant = false)]
		public float warpScaleFactor = 0.001f;
		
		[KSPField(isPersistant = true)]
		public float lastActive = -1f;

		public double GetAntimatter(CelestialBody body, double alt, double reallat)
		{
			bool atSun = (body.flightGlobalsIndex == 0);

			if (!atSun && body.atmosphere) {
				if (alt <= body.atmosphereDepth)
					return 0.0;
			}
				
			double baseAntimatter;
			if (body.name == "Kerbin") {
				baseAntimatter = 1.0;
			} else if (body.name == "Eve") {
				baseAntimatter = 2.5;
			} else if (body.name == "Moho") {
				baseAntimatter = 4.5;
			} else if (body.name == "Jool") {
				baseAntimatter = 8.0; 
			} else if (body.referenceBody && body.referenceBody.name == "Jool") {
				baseAntimatter = 5.0;
				if (body.name == "Tylo")
					baseAntimatter = 6.25;
			} else if (atSun) {
				baseAntimatter = 100.0;
			} else
				return 0.0;

			double bestAlt = 1.5 * body.Radius;
			double multiplier;
			if (alt <= bestAlt) {
				multiplier = alt / bestAlt;
			} else {
				multiplier = (bestAlt * bestAlt) / (alt * alt);
			}

			double latp = reallat / 180.0 * Math.PI;
			double anti = baseAntimatter * multiplier * (double)scaleFactor * Math.Abs(Math.Cos(latp));

			return anti;
		}

		public override void OnStart(StartState state) {
			if (state == StartState.Editor) { return; }

			double now = Planetarium.GetUniversalTime();
			if (IsActive && lastActive >= 0 && vessel.orbit.eccentricity <= 1) {
				double avgAlt = (vessel.orbit.ApR + vessel.orbit.PeR) / 2.0f;

				double avgInflow = (GetAntimatter(vessel.mainBody, avgAlt, vessel.orbit.inclination) + GetAntimatter(vessel.mainBody, avgAlt, 0.0));

				double collected = (now - lastActive) * avgInflow * warpScaleFactor;
				part.RequestResource(processInto, -1 * collected * processRatio);
			}

			SetEventsState();

		}
			
		public void FixedUpdate()
		{
			showInflow = "(inactive)";
			if (vessel != null && part != null && IsActive)
			{
				lastActive = (float)Planetarium.GetUniversalTime();

				double dt = (double)TimeWarp.fixedDeltaTime;
				double lat = vessel.mainBody.GetLatitude(this.vessel.GetWorldPos3D());
				double inflow = GetAntimatter(vessel.mainBody, vessel.altitude, lat);

				part.RequestResource(processInto, -inflow * dt * warpScaleFactor);

				showInflow = String.Format("{0:F2}", inflow);

			}
		}

		private void SetEventsState ()
		{
			if (IsActive) {
				Events ["OpenIntake"].active = false;
				Events ["CloseIntake"].active = true;
			}
			else {
				Events ["OpenIntake"].active = true;
				Events ["CloseIntake"].active = false;
			}
		}

		[KSPEvent(guiName = "Activate", guiActive = true)]
		public void OpenIntake()
		{
			if (!IsActive) {
				IsActive = true;
				SetEventsState ();
			}
		}

		[KSPEvent(guiName = "Deactivate", guiActive = true, active = false)]
		public void CloseIntake()
		{
			if (IsActive) {
				IsActive = false;
				SetEventsState ();
			}
		}

		[KSPAction("Activate")]
		public void OpenIntakeAction(KSPActionParam ap)
		{
			OpenIntake();
		}

		[KSPAction("Deactivate")]
		public void CloseIntakeAction(KSPActionParam ap)
		{
			CloseIntake();
		}



	}
}

