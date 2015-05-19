// This code is pretty much a direct copy of code from Firespitter
// (https://github.com/snjo/Firespitter)
// with a few hacks to make it self-contained and work in WarpShip

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace WarpShip
{
	public class WSXFSfuelSwitch : PartModule, IPartCostModifier
	{

		public class WSXFSresource
		{
			//public PartResource resource;
			public string name;
			public int ID;
			public float ratio;
			public double currentSupply = 0f;
			public double amount = 0f;
			public double maxAmount = 0f;

			public WSXFSresource(string _name, float _ratio)
			{
				name = _name;
				ID = _name.GetHashCode();
				ratio = _ratio;
			}

			public WSXFSresource(string _name)
			{
				name = _name;
				ID = _name.GetHashCode();
				ratio = 1f;
			}
		}

		public class WSXFSmodularTank
		{
			public List<WSXFSresource> resources = new List<WSXFSresource>();
		}

		private static List<double> WSXFSparseDoubles(string stringOfDoubles)
		{
			System.Collections.Generic.List<double> list = new System.Collections.Generic.List<double>();
			string[] array = stringOfDoubles.Trim().Split(';');
			for (int i = 0; i < array.Length; i++)
			{
				double item = 0f;
				if (double.TryParse(array[i].Trim(), out item))
				{
					list.Add(item);
				}
				else
				{
					Debug.Log("FStools: invalid float: [len:" + array[i].Length + "] '" + array[i]+ "']");
				}
			}
			return list;
		}

		private static List<string> WSXFSparseNames(string names)
		{
			return WSXFSparseNames(names, false, true, string.Empty);
		}

		private static List<string> WSXFSparseNames(string names, bool replaceBackslashErrors)
		{
			return WSXFSparseNames(names, replaceBackslashErrors, true, string.Empty);
		}

		private static List<string> WSXFSparseNames(string names, bool replaceBackslashErrors, bool trimWhiteSpace, string prefix)
		{
			List<string> source = names.Split(';').ToList<string>();
			for (int i = source.Count - 1; i >= 0; i--)
			{
				if (source[i] == string.Empty)
				{
					source.RemoveAt(i);
				}
			}
			if (trimWhiteSpace)
			{
				for (int i = 0; i < source.Count; i++)
				{                    
					source[i] = source[i].Trim(' ');                    
				}
			}
			if (prefix != string.Empty)
			{
				for (int i = 0; i < source.Count; i++)
				{
					source[i] = prefix + source[i];
				}
			}
			if (replaceBackslashErrors)
			{
				for (int i = 0; i < source.Count; i++)
				{
					source[i] = source[i].Replace('\\', '/');
				}
			}            
			return source.ToList<string>();
		}

		private static FieldInfo windowListField;

		/// <summary>
		/// Find the UIPartActionWindow for a part. Usually this is useful just to mark it as dirty.
		/// </summary>
		private static UIPartActionWindow WSXFSFindActionWindow(Part part)
		{
			if (part == null)
				return null;

			// We need to do quite a bit of piss-farting about with reflection to 
			// dig the thing out. We could just use Object.Find, but that requires hitting a heap more objects.
			UIPartActionController controller = UIPartActionController.Instance;
			if (controller == null)
				return null;

			if (windowListField == null)
			{
				Type cntrType = typeof(UIPartActionController);
				foreach (FieldInfo info in cntrType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
				{
					if (info.FieldType == typeof(List<UIPartActionWindow>))
					{
						windowListField = info;
						goto foundField;
					}
				}
				Debug.LogWarning("*PartUtils* Unable to find UIPartActionWindow list");
				return null;
			}
			foundField:

			List<UIPartActionWindow> uiPartActionWindows = (List<UIPartActionWindow>)windowListField.GetValue(controller);
			if (uiPartActionWindows == null)
				return null;

			return uiPartActionWindows.FirstOrDefault(window => window != null && window.part == part);
		}


		[KSPField]
		public string resourceNames = "ElectricCharge;LiquidFuel,Oxidizer;MonoPropellant";
		[KSPField]
		public string resourceAmounts = "100;75,25;200";
		[KSPField]
		public string initialResourceAmounts = "";
		[KSPField]
		public float basePartMass = 0.25f;
		[KSPField]
		public string tankMass = "0;0;0;0";
		[KSPField]
		public string tankCost = "0; 0; 0; 0";
		[KSPField]
		public bool displayCurrentTankCost = false;
		[KSPField]
		public bool hasGUI = true;
		[KSPField]
		public bool availableInFlight = false;
		[KSPField]
		public bool availableInEditor = true;

		[KSPField(isPersistant = true)]
		public int selectedTankSetup = -1;
		[KSPField(isPersistant = true)]
		public bool hasLaunched = false;
		[KSPField]
		public bool showInfo = true; // if false, does not feed info to the part list pop up info menu

		[KSPField(guiActive = false, guiActiveEditor = false, guiName = "Added cost")]
		public float addedCost = 0f;
		[KSPField(guiActive = false, guiActiveEditor = true, guiName = "Dry mass")]
		public float dryMassInfo = 0f;
		private List<WSXFSmodularTank> tankList;
		private List<double> weightList;
		private List<double> tankCostList;
		private bool initialized = false;
		[KSPField (isPersistant = true)]
		public bool configLoaded = false;

		UIPartActionWindow tweakableUI;        

		public override void OnStart(PartModule.StartState state)
		{            
			initializeData();
			if (selectedTankSetup == -1)
			{
				selectedTankSetup = 0;
				assignResourcesToPart(false);
			}
		}

		public override void OnAwake()
		{
			//Debug.Log("FS AWAKE "+initialized+" "+configLoaded+" "+resourceAmounts);
			if (configLoaded)
			{
				initializeData();
			}
			//Debug.Log("FS AWAKE DONE " + (configLoaded ? tankList.Count.ToString() : "NO CONFIG"));
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			//Debug.Log("FS LOAD " + initialized + " " + resourceAmounts+configLoaded);
			if (!configLoaded)
			{
				initializeData();
			}
			configLoaded = true;
			//Debug.Log("FS LOAD DONE " + tankList.Count);
		}

		private void initializeData()
		{
			try 
			{
				if (!initialized)
				{
					setupTankList(false);
					weightList = WSXFSparseDoubles(tankMass);
					tankCostList = WSXFSparseDoubles(tankCost);
					if (HighLogic.LoadedSceneIsFlight) hasLaunched = true;
					if (Events != null) {
						if (hasGUI) {
							Events ["nextTankSetupEvent"].guiActive = availableInFlight;
							Events ["nextTankSetupEvent"].guiActiveEditor = availableInEditor;

						} else {
							Events ["nextTankSetupEvent"].guiActive = false;
							Events ["nextTankSetupEvent"].guiActiveEditor = false;
						}
					}

					if (HighLogic.CurrentGame == null || HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
					{
						Fields["addedCost"].guiActiveEditor = displayCurrentTankCost;
					}

					initialized = true;
				}
			}
			catch
			{
				print ("Error in initializeData");
			}
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Switch Contents")]
		public void nextTankSetupEvent()
		{
			selectedTankSetup++;
			if (selectedTankSetup >= tankList.Count)
			{
				selectedTankSetup = 0;
			}
			assignResourcesToPart(true);            
		}

		public void selectTankSetup(int i, bool calledByPlayer)
		{            
			initializeData();
			if (selectedTankSetup != i)
			{
				selectedTankSetup = i;
				assignResourcesToPart(calledByPlayer);
			}
		}

		private void assignResourcesToPart(bool calledByPlayer)
		{            
			// destroying a resource messes up the gui in editor, but not in flight.
			setupTankInPart(part, calledByPlayer);
			if (HighLogic.LoadedSceneIsEditor)
			{
				for (int s = 0; s < part.symmetryCounterparts.Count; s++)
				{
					setupTankInPart(part.symmetryCounterparts[s], calledByPlayer);
					WSXFSfuelSwitch symSwitch = part.symmetryCounterparts[s].GetComponent<WSXFSfuelSwitch>();
					if (symSwitch != null)
					{
						symSwitch.selectedTankSetup = selectedTankSetup;
					}
				}
			}

			//Debug.Log("refreshing UI");

			if (tweakableUI == null)
			{
				tweakableUI = WSXFSFindActionWindow(part);
			}
			if (tweakableUI != null)
			{
				tweakableUI.displayDirty = true;
			}
			else
			{
				Debug.Log("no UI to refresh");
			}
		}

		private void setupTankInPart(Part currentPart, bool calledByPlayer)
		{
			currentPart.Resources.list.Clear();
			PartResource[] partResources = currentPart.GetComponents<PartResource>();
			for (int i = 0; i < partResources.Length; i++)
			{
				DestroyImmediate(partResources[i]);
			}            

			for (int tankCount = 0; tankCount < tankList.Count; tankCount++)
			{
				if (selectedTankSetup == tankCount)
				{
					for (int resourceCount = 0; resourceCount < tankList[tankCount].resources.Count; resourceCount++)
					{
						if (tankList[tankCount].resources[resourceCount].name != "Structural")
						{
							//Debug.Log("new node: " + tankList[i].resources[j].name);
							ConfigNode newResourceNode = new ConfigNode("RESOURCE");
							newResourceNode.AddValue("name", tankList[tankCount].resources[resourceCount].name);
							newResourceNode.AddValue("maxAmount", tankList[tankCount].resources[resourceCount].maxAmount);
							if (calledByPlayer && !HighLogic.LoadedSceneIsEditor)
							{
								newResourceNode.AddValue("amount", 0.0f);
							} 
							else
							{
								newResourceNode.AddValue("amount", tankList[tankCount].resources[resourceCount].amount);
							}

							//Debug.Log("add node to part");
							currentPart.AddResource(newResourceNode);                          
						}
						else
						{
							//Debug.Log("Skipping structural fuel type");
						}
					}
				}
			}
			currentPart.Resources.UpdateList();
			updateWeight(currentPart, selectedTankSetup);
			updateCost();
		}

		private float updateCost()
		{
			if (selectedTankSetup >= 0 && selectedTankSetup < tankCostList.Count)
			{
				addedCost = (float)tankCostList[selectedTankSetup];
			}
			else
			{
				addedCost = 0f;
			}
			//GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship); //crashes game
			return addedCost;
		}

		private void updateWeight(Part currentPart, int newTankSetup)
		{
			if (newTankSetup < weightList.Count)
			{
				currentPart.mass = (float)(basePartMass + weightList[newTankSetup]);
			}
		}

		public override void OnUpdate()
		{
		}

		public void Update()
		{
			if (HighLogic.LoadedSceneIsEditor)
			{
				dryMassInfo = part.mass;
			}
		}

		public void setupTankList(bool calledByPlayer)
		{
			tankList = new List<WSXFSmodularTank>();
			weightList = new List<double>();
			tankCostList = new List<double>();

			// First find the amounts each tank type is filled with

			List<List<double>> resourceList = new List<List<double>>();
			List<List<double>> initialResourceList = new List<List<double>>();
			string[] resourceTankArray = resourceAmounts.Split(';');
			string[] initialResourceTankArray = initialResourceAmounts.Split(';');
			if (initialResourceAmounts.Equals("") ||
				initialResourceTankArray.Length != resourceTankArray.Length)
			{
				initialResourceTankArray = resourceTankArray;
			}
			//Debug.Log("FSDEBUGRES: " + resourceTankArray.Length+" "+resourceAmounts);
			for (int tankCount = 0; tankCount < resourceTankArray.Length; tankCount++)
			{
				resourceList.Add(new List<double>());
				initialResourceList.Add(new List<double>());
				string[] resourceAmountArray = resourceTankArray[tankCount].Trim().Split(',');
				string[] initialResourceAmountArray = initialResourceTankArray[tankCount].Trim().Split(',');
				if (initialResourceAmounts.Equals("") ||
					initialResourceAmountArray.Length != resourceAmountArray.Length)
				{
					initialResourceAmountArray = resourceAmountArray;
				}
				for (int amountCount = 0; amountCount < resourceAmountArray.Length; amountCount++)
				{
					try
					{
						resourceList[tankCount].Add(double.Parse(resourceAmountArray[amountCount].Trim()));
						initialResourceList[tankCount].Add(double.Parse(initialResourceAmountArray[amountCount].Trim()));
					}
					catch
					{
						Debug.Log("FSfuelSwitch: error parsing resource amount " + tankCount + "/" + amountCount + ": '" + resourceTankArray[amountCount] + "': '" + resourceAmountArray[amountCount].Trim()+"'");
					}
				}
			}

			// Then find the kinds of resources each tank holds, and fill them with the amounts found previously, or the amount hey held last (values kept in save persistence/craft)

			string[] tankArray = resourceNames.Split(';');
			for (int tankCount = 0; tankCount < tankArray.Length; tankCount++)
			{
				WSXFSmodularTank newTank = new WSXFSmodularTank();
				tankList.Add(newTank);
				string[] resourceNameArray = tankArray[tankCount].Split(',');
				for (int nameCount = 0; nameCount < resourceNameArray.Length; nameCount++)
				{
					WSXFSresource newResource = new WSXFSresource(resourceNameArray[nameCount].Trim(' '));
					if (resourceList[tankCount] != null)
					{
						if (nameCount < resourceList[tankCount].Count)
						{
							newResource.maxAmount = resourceList[tankCount][nameCount];
							newResource.amount    = initialResourceList[tankCount][nameCount];
						}
					}
					newTank.resources.Add(newResource);
				}
			}            
		}

		public float GetModuleCost()
		{
			return updateCost();
		}
		public float GetModuleCost(float modifier)
		{
			return updateCost();
		}

		public override string GetInfo()
		{
			if (showInfo)
			{
				List<string> resourceList = WSXFSparseNames(resourceNames);
				StringBuilder info = new StringBuilder();
				info.AppendLine("Fuel tank setups available:");
				for (int i = 0; i < resourceList.Count; i++)
				{
					info.AppendLine(resourceList[i].Replace(",", ", "));
				}
				return info.ToString();
			}
			else
				return string.Empty;
		}
	}    
}

