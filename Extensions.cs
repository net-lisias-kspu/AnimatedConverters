//   Extensions.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

namespace AT_Utils
{
	public static class AnimatedConvertersExtensions
	{
		public static void ConfigurationInvalid(this PartModule pm, string msg, params object[] args)
		{
			Utils.Message(6, "WARNING: {0}.\n" +
			              "Configuration of \"{1}\" is INVALID.", 
			              string.Format(msg, args), 
			              pm.Title());
			pm.enabled = pm.isEnabled = false;
			return;
		}

		public static PartResourceDefinition GetResourceDef(this PartModule pm, string name)
		{
			var res = PartResourceLibrary.Instance.GetDefinition(name);
			if(res == null) 
				pm.ConfigurationInvalid("no '{0}' resource in the library", name);
			return res;
		}
	}
}

