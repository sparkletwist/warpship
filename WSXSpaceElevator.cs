/*
 * This doesn't work
 * 

using System;

namespace WarpShip
{
	public class SpaceElevator : LaunchClamp
	{
		[KSPField(isPersistant = true)]
		public bool Lift = false;

		[KSPField] 
		public float ClimbRate = 100f;

		public override void OnActive() {
			Lift = true;
		}

		void FixedUpdate() {
			if (Lift) {
				double dt = TimeWarp.fixedDeltaTime;
				base.height += ClimbRate * (float)dt;
			}
			base.OnFixedUpdate();
		}
	}
}
*/

