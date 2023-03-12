using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharedProject
{
	internal class BlueprintAsset
	{
		public string blueprintasset { get; set; } = null;

		public int Deserialize(ref MemoryStream memorystream)  // returns the last character read from the stream
		{
			int somechar = -1;

			try
			{
				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return -1;
				}

				if (somechar == ']')  // end of an empty array?
				{
					return somechar;
				}

				if (somechar != '{')
				{
					return -1;
				}

				string token = SharedGlobals.ReadToken(ref memorystream);

				if (token != "bpasset")
				{
					return -1;
				}

				string Value = SharedGlobals.ReadValue(ref memorystream);

				if (Value == "")
				{
					return -1;
				}

				blueprintasset = Value;

				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return -1;
				}

				if (somechar != '}')
				{
					return -1;
				}
			}
			catch(Exception)
			{
			}

			return somechar;  // should be '}'
		}
	}

	internal class BlueprintAssetIndex
	{
		public int blueprintassetindex { get; set; } = -1;
		public int count { get; set; } = 0;

		public int Deserialize(ref MemoryStream memorystream)  // returns the last character read from the stream
		{
			int somechar = -1;

			try
			{
				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return -1;
				}

				if (somechar == ']')  // end of an empty array?
				{
					return somechar;
				}

				if (somechar != '{')
				{
					return -1;
				}

				string token = SharedGlobals.ReadToken(ref memorystream);

				if (token != "bpasset")
				{
					return -1;
				}

				string Value = SharedGlobals.ReadValue(ref memorystream);

				if (Value == "")
				{
					return -1;
				}

				blueprintassetindex = int.Parse(Value);

				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return -1;
				}

				if (somechar != ',')
				{
					return -1;
				}

				token = SharedGlobals.ReadToken(ref memorystream);

				if (token != "count")
				{
					return -1;
				}

				Value = SharedGlobals.ReadValue(ref memorystream);

				if (Value == "")
				{
					return -1;
				}

				count = int.Parse(Value);

				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return -1;
				}

				if (somechar != '}')
				{
					return -1;
				}
			}
			catch(Exception)
			{
			}

			return somechar;  // should be '}'
		}
	}

	internal class BlueprintFunction
	{
		public string functionname { get; set; } = null;
		public List<BlueprintAssetIndex> blueprintassetindexes { get; set; } = null;

		public int Deserialize(ref MemoryStream memorystream)  // returns the last character read from the stream
		{
			int somechar = -1;

			try
			{
				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return -1;
				}

				if (somechar == ']')  // end of an empty array?
				{
					return somechar;
				}

				if (somechar != '{')
				{
					return -1;
				}

				string token = SharedGlobals.ReadToken(ref memorystream);

				if (token != "func")
				{
					return -1;
				}

				string Value = SharedGlobals.ReadValue(ref memorystream);

				if (Value == "")
				{
					return -1;
				}

				functionname = Value;

				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return -1;
				}

				if (somechar != ',')
				{
					return -1;
				}

				token = SharedGlobals.ReadToken(ref memorystream);

				if (token != "bps")
				{
					return -1;
				}

				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return -1;
				}

				if (somechar != '[')  // look for the start of the array
				{
					return -1;
				}

				blueprintassetindexes = new List<BlueprintAssetIndex>();

				// loop through each function here and add it to the List
				BlueprintAssetIndex someblueprintassetindex = new BlueprintAssetIndex();
				somechar = someblueprintassetindex.Deserialize(ref memorystream);

				if (somechar == -1)
				{
				}

				while ((somechar != -1) && (somechar != ']'))  // loop until end of buffer or until end of array
				{
					blueprintassetindexes.Add(someblueprintassetindex);

					if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
					{
						return -1;
					}
					if (somechar != ',')
					{
						break;
					}

					someblueprintassetindex = new BlueprintAssetIndex();
					somechar = someblueprintassetindex.Deserialize(ref memorystream);
				}

				if (somechar != ']')  // look for the end of the array
				{
					return -1;
				}

				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return -1;
				}

				if (somechar != '}')
				{
					return -1;
				}
			}
			catch(Exception)
			{
			}

			return somechar;  // should be '}'
		}
	}

	internal class UnrealClass
	{
		public string classname { get; set; }
		public List<BlueprintFunction> functions { get; set; }
		public Dictionary<string, int> FunctionNameToIndex = null;

		public int Deserialize(ref MemoryStream memorystream)  // returns the last character read from the stream
		{
			int somechar = -1;

			try
			{
				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return -1;
				}

				if (somechar != '{')
				{
					return -1;
				}

				string token = SharedGlobals.ReadToken(ref memorystream);

				if (token != "class")
				{
					return -1;
				}

				string Value = SharedGlobals.ReadValue(ref memorystream);

				if (Value == "")
				{
					return -1;
				}

				classname = Value;

				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return -1;
				}

				if (somechar != ',')
				{
					return -1;
				}

				token = SharedGlobals.ReadToken(ref memorystream);

				if (token != "functions")
				{
					return -1;
				}

				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return -1;
				}

				if (somechar != '[')  // look for the start of the array
				{
					return -1;
				}

				functions = new List<BlueprintFunction>();

				// loop through each function here and add it to the List
				BlueprintFunction somefunction = new BlueprintFunction();
				somechar = somefunction.Deserialize(ref memorystream);

				while ((somechar != -1) && (somechar != ']'))  // loop until end of buffer or until end of array
				{
					functions.Add(somefunction);

					if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
					{
						return -1;
					}
					if (somechar != ',')
					{
						break;
					}

					somefunction = new BlueprintFunction();
					somechar = somefunction.Deserialize(ref memorystream);
				}

				if (somechar != ']')  // look for the end of the array
				{
					return -1;
				}

				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return -1;
				}

				if (somechar != '}')
				{
					return -1;
				}
			}
			catch(Exception)
			{
			}

			// build the dictionary for quick lookup of function name
			FunctionNameToIndex = new Dictionary<string, int>();
			for (int index = 0; index < functions.Count; ++index)
			{
				FunctionNameToIndex.Add(functions[index].functionname, index);
			}

			return somechar;
		}
	}

	internal class BlueprintJson
	{
		private List<BlueprintAsset> blueprintassets { get; set; } = null;

		private Int32 Version;

		public List<UnrealClass> classes { get; set; } = null;
		private Dictionary<string, int> ClassNameToIndex = null;

		public BlueprintJson()
		{
		}

		public void ReadBlueprintJson(string filename)
		{
			if (File.Exists(filename))
			{
				byte[] jsonfile = File.ReadAllBytes(filename);

				MemoryStream memorystream = new MemoryStream(jsonfile);
				{
					Deserialize(ref memorystream);
				}
				memorystream.Dispose();
			}
		}

		public bool Deserialize(ref MemoryStream memorystream)  // returns bool indicating success status
		{
			int somechar = -1;

			try
			{
				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return false;
				}

				if (somechar != '{')
				{
					return false;
				}

				// read the json version number...
				string token = SharedGlobals.ReadToken(ref memorystream);

				if (token != "version")
				{
					return false;
				}

				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return false;
				}

				if (somechar != '{')
				{
					return false;
				}

				token = SharedGlobals.ReadToken(ref memorystream);

				if (token != "version")
				{
					return false;
				}

				string Value = SharedGlobals.ReadValue(ref memorystream);

				if (Value == "")
				{
					return false;
				}

				Version = int.Parse(Value);

				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return false;
				}

				if (somechar != '}')
				{
					return false;
				}

				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return false;
				}

				if (somechar != ',')
				{
					return false;
				}

				token = SharedGlobals.ReadToken(ref memorystream);

				if (token != "blueprints")
				{
					return false;
				}

				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return false;
				}

				if (somechar != '[')  // look for the start of the array
				{
					return false;
				}

				blueprintassets = new List<BlueprintAsset>();

				// loop through each blueprintasset here and add it to the List
				BlueprintAsset someblueprintasset = new BlueprintAsset();
				somechar = someblueprintasset.Deserialize(ref memorystream);

				while ((somechar != -1) && (somechar != ']'))  // loop until end of buffer or until end of array
				{
					blueprintassets.Add(someblueprintasset);

					if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
					{
						return false;
					}
					if (somechar != ',')
					{
						break;
					}

					someblueprintasset = new BlueprintAsset();
					somechar = someblueprintasset.Deserialize(ref memorystream);
				}

				if (somechar != ']')  // look for the end of the array
				{
					return false;
				}

				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return false;
				}
				if (somechar != ',')
				{
					return false;
				}

				token = SharedGlobals.ReadToken(ref memorystream);

				if (token != "classes")
				{
					return false;
				}

				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return false;
				}

				if (somechar != '[')  // look for the start of the array
				{
					return false;
				}

				classes = new List<UnrealClass>();

				// loop through each class here and add it to the List
				UnrealClass someclass = new UnrealClass();
				somechar = someclass.Deserialize(ref memorystream);

				while ((somechar != -1) && (somechar != ']'))  // loop until end of buffer or until end of array
				{
					classes.Add(someclass);

					if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
					{
						return false;
					}
					if (somechar != ',')
					{
						break;
					}

					someclass = new UnrealClass();
					somechar = someclass.Deserialize(ref memorystream);
				}

				if (somechar != ']')  // look for the end of the array
				{
					return false;
				}

				if ((somechar = SharedGlobals.ReadCharSkipWhitespace(ref memorystream)) == -1)
				{
					return false;
				}

				if (somechar != '}')
				{
					return false;
				}
			}
			catch(Exception)
			{
			}

			// build the dictionary for quick lookup of class name
			ClassNameToIndex = new Dictionary<string, int>();
			for (int index = 0; index < classes.Count; ++index)
			{
				ClassNameToIndex.Add(classes[index].classname, index);
			}

			return true;
		}

		public int GetClassIndex(string class_name)
		{
			if (class_name == null)
			{
				return -1;
			}

			string class_name_lower = class_name.ToLower();

			int classname_index = -1;
			if (ClassNameToIndex != null && ClassNameToIndex.TryGetValue(class_name_lower, out classname_index))
			{
				return classname_index;
			}

			return -1;
		}

		public int GetFunctionIndex(int class_index, string function_name)
		{
			if (function_name == null)
			{
				return -1;
			}

			string function_name_lower = function_name.ToLower();

			if (class_index < classes.Count && classes[class_index].FunctionNameToIndex != null)
			{
				int function_name_index = -1;
				if (classes[class_index].FunctionNameToIndex.TryGetValue(function_name_lower, out function_name_index))
				{
					return function_name_index;
				}
			}

			return -1;
		}

		public List<string> GetBlueprintAssetPaths(int class_index, int function_index)
		{
			List<string> list = new List<string>();

			if (classes == null)
			{
				return list;
			}

			if (class_index < classes.Count)
			{
				if (classes[class_index].functions == null)
				{
					return list;
				}

				if (function_index < classes[class_index].functions.Count)
				{
					if (classes[class_index].functions[function_index].blueprintassetindexes == null)
					{
						return list;
					}

					for (int index = 0; index < classes[class_index].functions[function_index].blueprintassetindexes.Count; ++index)
					{
						int asset_index = classes[class_index].functions[function_index].blueprintassetindexes[index].blueprintassetindex;
						if (blueprintassets != null && asset_index < blueprintassets.Count())
						{
							string path = String.Format("{0} ({1})", blueprintassets[asset_index].blueprintasset, classes[class_index].functions[function_index].blueprintassetindexes[index].count);
							list.Add(path);
						}
					}
				}
			}

			list.Sort();

			return list;
		}

		public bool Compare(BlueprintJson otherBlueprintJson)
		{
			if (classes.Count != otherBlueprintJson.classes.Count)
			{
				return false;
			}

			for (int class_index = 0; class_index < classes.Count; ++class_index)
			{
				// classes are sorted so these should match if the code is unchanged
				if (classes[class_index].classname != otherBlueprintJson.classes[class_index].classname)
				{
					return false;
				}

				if (classes[class_index].functions.Count != otherBlueprintJson.classes[class_index].functions.Count)
				{
					return false;
				}

				for (int function_index = 0; function_index < classes[class_index].functions.Count; ++function_index)
				{
					if (classes[class_index].functions[function_index].functionname != otherBlueprintJson.classes[class_index].functions[function_index].functionname)
					{
						return false;
					}
				}
			}

			return true;
		}

	}
}
