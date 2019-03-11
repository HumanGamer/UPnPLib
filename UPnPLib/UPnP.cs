using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UPnPLib
{
	// ReSharper disable once InconsistentNaming
	public class UPnP
    {
	    private static Gateway _defaultGateway;
		private static readonly GatewayFinder Finder;

		static UPnP()
		{
			_defaultGateway = null;
			Finder = new GatewayFinder();
			Finder.OnGatewayFound += gateway =>
			{
				if (_defaultGateway == null)
					_defaultGateway = gateway;
			};
		}

		/// <summary>
		/// Waits for UPnP to be initialized (takes ~3 seconds).<br/>
		/// It is not necessary to call this method manually before using UPnP functions
		/// </summary>
		public static void WaitForInitialization()
		{
			while (Finder.IsSearching())
			{
				Thread.Sleep(1);
			}
		}
		
		/// <summary>
		/// Is there a UPnP gateway?<br/>
		/// This method is blocking if UPnP is still initializing.<br/>
		/// All UPnP commands will fail if UPnP is not available
		/// </summary>
		/// <returns>true if available, false if not</returns>
		// ReSharper disable once InconsistentNaming
		public static bool IsUPnPAvailable()
		{
			WaitForInitialization();
			return _defaultGateway != null;
		}

		/// <summary>
		/// Opens a TCP port on the gateway
		/// </summary>
		/// <param name="port">TCP port (0-65535)</param>
		/// <returns>true if the operation was successful, false otherwise</returns>
		public static bool OpenPortTCP(int port)
		{
			if (!IsUPnPAvailable())
				return false;

			return _defaultGateway.OpenPort(port, false);
		}

		/// <summary>
		/// Opens a UDP port on the gateway
		/// </summary>
		/// <param name="port">UDP port (0-65535)</param>
		/// <returns>true if the operation was successful, false otherwise</returns>
		public static bool OpenPortUDP(int port)
		{
			if (!IsUPnPAvailable())
				return false;

			return _defaultGateway.OpenPort(port, true);
		}

		/// <summary>
		/// Closes a TCP port on the gateway
		/// </summary>
		/// <param name="port">TCP port (0-65535)</param>
		/// <returns>true if the operation was successful, false otherwise</returns>
		public static bool ClosePortTCP(int port)
		{
			if (!IsUPnPAvailable())
				return false;

			return _defaultGateway.ClosePort(port, false);
		}

		/// <summary>
		/// Closes a UDP port on the gateway
		/// </summary>
		/// <param name="port">UDP port (0-65535)</param>
		/// <returns>true if the operation was successful, false otherwise</returns>
		public static bool ClosePortUDP(int port)
		{
			if (!IsUPnPAvailable())
				return false;

			return _defaultGateway.ClosePort(port, true);
		}

		/// <summary>
		/// Checks if a TCP port is mapped
		/// </summary>
		/// <param name="port">TCP port (0-65535)</param>
		/// <returns>true if the port is mapped, false otherwise</returns>
		public static bool IsMappedTCP(int port)
		{
			if (!IsUPnPAvailable())
				return false;

			return _defaultGateway.IsMapped(port, false);
		}

		/// <summary>
		/// Checks if a UDP port is mapped
		/// </summary>
		/// <param name="port">UDP port (0-65535)</param>
		/// <returns>true if the port is mapped, false otherwise</returns>
		public static bool IsMappedUDP(int port)
		{
			if (!IsUPnPAvailable())
				return false;

			return _defaultGateway.IsMapped(port, true);
		}

		/// <summary>
		/// Gets the external IP address of the default gateway
		/// </summary>
		/// <returns>external IP address as string, or null if not available</returns>
		public static string GetExternalIP()
		{
			if (!IsUPnPAvailable())
				return null;

			return _defaultGateway.GetExternalIP();
		}

		/// <summary>
		/// Gets the internal IP address of this machine
		/// </summary>
		/// <returns>internal IP address as string, or null if not available</returns>
		public static string GetLocalIP()
		{
			if (!IsUPnPAvailable())
				return null;

			return _defaultGateway.GetLocalIP();
		}
	}
}
