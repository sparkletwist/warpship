using System;
using UnityEngine;

namespace WarpShip
{
	public class AtmosphericScoop : PartModule
	{
		[KSPField(isPersistant = false)]
		public string worksAt = "Jool";

		[KSPField(isPersistant = false)]
		public string scoops = "JoolGas";

		[KSPField(isPersistant = false)]
		public string intakeTransformName = "Intake";

		[KSPField(isPersistant = true)]
		public bool intakeOpen = false;

		[KSPField(isPersistant = true)]
		public float area = 0.0085f;

		[KSPField(isPersistant = true)]
		public float intakeSpeed = 10f;

		[KSPField(isPersistant = true)]
		public float intakeConstant = 1.0f;

		[KSPField(guiName = "Status", guiActive = true, isPersistant = false)]
		public string status = "Nominal";

		[KSPField(guiName = "Flow", guiActive = true, isPersistant = false, guiFormat = "F2", guiUnits = "U")]
		public float inflow = 0f;

		[KSPField(guiName = "Speed", guiActive = true, isPersistant = false, guiFormat = "F2", guiUnits = "M/s")]
		public float showSpeed = 0f;

		private Transform storedTransform = null;
		private bool noTransform = false;

		public override void OnStart(StartState state)
		{
			if (intakeOpen) {
				Events ["OpenIntake"].active = false;
				Events ["CloseIntake"].active = true;
			} else {
				Events ["OpenIntake"].active = true;
				Events ["CloseIntake"].active = false;
			}
		}

		[KSPEvent(guiName = "Open Intake", guiActive = true)]
		public void OpenIntake()
		{
			if (!intakeOpen) {
				intakeOpen = true;
				Events ["OpenIntake"].active = false;
				Events ["CloseIntake"].active = true;
			}
		}

		[KSPEvent(guiName = "Close Intake", guiActive = true, active = false)]
		public void CloseIntake()
		{
			if (intakeOpen) {
				intakeOpen = false;
				Events ["OpenIntake"].active = true;
				Events ["CloseIntake"].active = false;
			}
		}

		[KSPAction("Open Intake")]
		public void OpenIntakeAction(KSPActionParam ap)
		{
			OpenIntake();
		}

		[KSPAction("Close Intake")]
		public void CloseIntakeAction(KSPActionParam ap)
		{
			CloseIntake();
		}

		public override string GetInfo()
		{
			return String.Format ("<b>Designed For:</b> {0}", worksAt);
		}

		public void FixedUpdate() 
		{
			double dt = TimeWarp.fixedDeltaTime;
			Vector3 surfaceVelocity = vessel.GetSrfVelocity ();
			double speed = surfaceVelocity.magnitude;
			showSpeed = (float)speed;

			inflow = 0f;

			if (noTransform || !intakeOpen) {
				status = "Closed";
				return;
			}

			if (part.ShieldedFromAirstream) {
				status = "Blocked";
				return;
			}

			string planet = vessel.mainBody.name;
			if (planet != worksAt) {
				status = "No Intake";
				return;
			}

			status = "Nominal";

			Transform tr;
			if (storedTransform == null) {
				tr = part.FindModelTransform (intakeTransformName);
				storedTransform = tr;
				if (!tr) {
					noTransform = true;
					return;
				}
			} else
				tr = storedTransform;


			float intakeScale = 1.0f;
			Vector3 nFwd = tr.forward.normalized;
			Vector3 nSurf = surfaceVelocity.normalized;

			float ang = Vector3.Angle (nFwd, nSurf);
			if (ang > 90f) {
				intakeScale = (180f - ang) / 100f;
			}
			float extraIntakeStrength = 0.2f;
			if (ang < 90f) {
				extraIntakeStrength += (0.8f*Vector3.Dot (nFwd, nSurf));
			}
				
			double density = vessel.atmDensity;
			PartResourceDefinition res = PartResourceLibrary.Instance.GetDefinition (scoops);
			double grabDensity = (density / res.density) * intakeScale * extraIntakeStrength * area * speed;

			inflow = (float)grabDensity;

			double gather = inflow * dt * intakeConstant * intakeSpeed;
			part.Resources [scoops].amount = Mathf.Clamp ((float)gather, 0f, (float)part.Resources [scoops].maxAmount);
		}
	}
}

