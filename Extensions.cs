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
		public static PartResourceDefinition GetResourceDef(this PartModule pm, string name)
		{
			var res = PartResourceLibrary.Instance.GetDefinition(name);
			if(res == null) 
				pm.ConfigurationInvalid("no '{0}' resource in the library", name);
			return res;
		}
	}
}

