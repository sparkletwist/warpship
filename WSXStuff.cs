using System;
using System.Collections.Generic;
using UnityEngine;

namespace WarpShip
{
	public class WSXStuff : MonoBehaviour
	{
		private const float builtinExplosionRadius = 8f;
		private const float builtinExplosionMultiplier = 8000f;

		public static void RedAlert(Vessel vessel) {
			if (vessel != null) {
				foreach (Part p in vessel.parts) {
					WarpDrive wd = p.FindModuleImplementing<WarpDrive> ();
					if (wd) {
						wd.CallRedAlert ();
						return;
					}
				}
			}
		}

		public static void PowerfulExplosion(Part p, float explosionMultiplier = 1f, float explosionRadius = 1f)
		{
			float power = explosionMultiplier * builtinExplosionMultiplier;
			float radius = explosionRadius * builtinExplosionRadius;
			Vector3 explosionCenter = p.transform.position;

			Collider[] boom = Physics.OverlapSphere (explosionCenter, radius);
		
			Rigidbody baserb = p.rigidbody;
			p.explode ();

			for (var i = 0; i < boom.Length; i++) {
				if (boom[i].attachedRigidbody && boom[i].attachedRigidbody != baserb) {
					boom [i].attachedRigidbody.AddExplosionForce (power, explosionCenter, radius);
				}
			}

		}

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
}
