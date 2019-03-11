using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UPnPLib;

namespace UPnPTester
{
	public class Program
	{
		public static void Main(string[] args)
		{
			Console.WriteLine("Testing UPnP...");
			if (UPnP.IsUPnPAvailable())
				Console.WriteLine("Found a UPnP-enabled router, your local IP is: " + UPnP.GetLocalIP() + ", and your external IP is: " + UPnP.GetExternalIP());
			else
				Console.WriteLine("Could not find a UPnP-enabled router");

			Console.Write("Press any key to continue...");
			Console.ReadKey();
		}
	}
}
