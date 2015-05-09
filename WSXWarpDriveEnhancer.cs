using System;

namespace WarpShip
{
	public class WarpDriveEnhancer : PartModule
	{
		public int effectiveness = 10;

		public bool AreYouHere()
		{
			return true;
		}

		public int GetEffectiveness(WarpDrive wd)
		{
			return effectiveness;
		}
	}
}

