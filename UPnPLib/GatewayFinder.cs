using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UPnPLib
{
	internal class GatewayFinder
	{
		public delegate void GatewayFound(Gateway gateway);

		public event GatewayFound OnGatewayFound;

		private static readonly string[] SearchMessages;

		static GatewayFinder()
		{
			var result = new List<string>();

			// ReSharper disable once LoopCanBeConvertedToQuery
			foreach (var type in new[]
			{
				"urn:schemas-upnp-org:device:InternetGatewayDevice:1",
				"urn:schemas-upnp-org:service:WANIPConnection:1",
				"urn:schemas-upnp-org:service:WANPPPConnection:1"
			})
			{
				result.Add("M-SEARCH * HTTP/1.1\r\n" +
						   "HOST: 239.255.255.250:1900\r\n" +
						   "ST: " + type + "\r\n" +
						   "MAN: \"ssdp:discover\"\r\n" +
						   "MX: 2\r\n\r\n");
			}

			SearchMessages = result.ToArray();
		}

		private class GatewayListener
		{
			private readonly IPAddress _ip;
			private readonly string _request;
			private readonly GatewayFinder _finder;
			private Task _task;

			public GatewayListener(IPAddress ip, string req, GatewayFinder finder)
			{
				_ip = ip;
				_request = req;
				_finder = finder;
			}

			private void Run()
			{
				try
				{
					byte[] req = Encoding.ASCII.GetBytes(_request);
					Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
					{
						ReceiveTimeout = 3000,
						SendTimeout = 3000
					};
					
					socket.Bind(new IPEndPoint(_ip, 0));
					socket.SendTo(req, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900));

					byte[] buffer = new byte[0x600];

					socket.Receive(buffer);

					string recv = Encoding.ASCII.GetString(buffer);
					Gateway gateway = new Gateway(recv, _ip);
					_finder.OnGatewayFound?.Invoke(gateway);
					_finder._found = true;
				}
				catch
				{
					// ignored
				}
			}

			public void Start()
			{
				_task = Task.Run(() => Run());
			}

			public bool IsAlive()
			{
				return _task != null && (_task.Status == TaskStatus.Running || _task.Status == TaskStatus.WaitingToRun);
			}

			public override string ToString()
			{
				return IsAlive() ? "Running" : "Idle";
			}
		}

		private readonly List<GatewayListener> _listeners = new List<GatewayListener>();
		
		// ReSharper disable once FieldCanBeMadeReadOnly.Local
		private bool _searching;
		private bool _found;

		public GatewayFinder()
		{
			_found = false;
			_searching = true;
			foreach (var ip in GetLocalIPs())
			{
				foreach (string req in SearchMessages)
				{
					GatewayListener listener = new GatewayListener(ip, req, this);
					listener.Start();
					_listeners.Add(listener);
				}
			}

			_searching = false;
		}

		public bool IsSearching()
		{
			if (_found)
				return false;

			if (_searching)
				return true;

			foreach (GatewayListener listener in _listeners)
			{
				if (listener.IsAlive())
					return true;
			}

			return false;
		}

		private static IPAddress[] GetLocalIPs()
		{
			List<IPAddress> ret = new List<IPAddress>();

			try
			{
				foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
				{
					try
					{
						bool virt = false; // TODO: check if virtual
						NetworkInterfaceType type = iface.NetworkInterfaceType;
						if (iface.OperationalStatus != OperationalStatus.Up ||
						    type.HasFlag(NetworkInterfaceType.Loopback) ||
						    virt ||
						    type.HasFlag(NetworkInterfaceType.Ppp))
							continue;
						var addresses = iface.GetIPProperties().UnicastAddresses;
						if (addresses == null || addresses.Count == 0)
							continue;

						foreach (var addr in addresses)
						{
							if (addr.Address.AddressFamily == AddressFamily.InterNetwork ||
							    addr.Address.AddressFamily == AddressFamily.InterNetworkV6)
							{
								if (ret.Contains(addr.Address))
									continue;
								ret.Add(addr.Address);
							}
						}
					}
					catch
					{
						// ignored
					}
				}
			}
			catch
			{
				// ignored
			}

			return ret.ToArray();
		}
	}
}