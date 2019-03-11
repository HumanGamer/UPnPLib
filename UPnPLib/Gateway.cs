using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace UPnPLib
{
	internal class Gateway
	{
		private readonly IPAddress _interface;

		private readonly string _serviceType = null;
		private readonly string _controlURL = null;

		public Gateway(string data, IPAddress ip)
		{
			_interface = ip;

			string location = null;

			string[] lines = data.Split(new char[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);

			foreach (string l in lines)
			{
				string line = l.Trim();

				if (line.Length == 0 || line.StartsWith("HTTP/1.") || line.StartsWith("NOTIFY *"))
					continue;

				if (!line.Contains(':'))
					continue;

				string name = line.Substring(0, line.IndexOf(':'));
				string val = line.Length >= name.Length ? line.Substring(name.Length + 1).Trim() : null;
				
				if (name.ToLower() == "location")
				{
					location = val;
					break;
				}
			}

			if (location == null)
				throw new Exception("Unsupported Gateway");

			XDocument doc = XDocument.Load(location);
			var services = (from d in doc.Descendants()
				where d.Name.LocalName == "service"
				select d);
			foreach (XElement service in services)
			{
				string serviceType = null;
				string controlURL = null;

				foreach (var node in service.Nodes())
				{
					XElement ele = node as XElement;
					if (ele == null)
						continue;

					if (ele.Name.LocalName.Trim().ToLower() == "servicetype" && ele.FirstNode is XText n1)
						serviceType = n1.Value;
					else if (ele.Name.LocalName.Trim().ToLower() == "controlurl" && ele.FirstNode is XText n2)
						controlURL = n2.Value;
				}

				if (serviceType == null || controlURL == null)
					continue;

				if (serviceType.Trim().ToLower().Contains(":wanipconnection:") ||
				    serviceType.Trim().ToLower().Contains(":wanpppconnection:"))
				{
					this._serviceType = serviceType.Trim();
					this._controlURL = controlURL.Trim();
				}
			}

			if (_controlURL == null)
				throw new Exception("Unsupported Gateway");

			int slash = location.IndexOf('/', 7); // finds the first slash after http://
			if (slash == -1)
				throw new Exception("Unsupported Gateway");

			location = location.Substring(0, slash);

			if (!_controlURL.StartsWith("/"))
				_controlURL = "/" + _controlURL;

			_controlURL = location + _controlURL;
		}

		private Dictionary<string, string> Command(string action, Dictionary<string, string> args = null)
		{
			Dictionary<string, string> ret = new Dictionary<string, string>();

			string soap = "<?xml version=\"1.0\"?>\r\n" +
			              "<SOAP-ENV:Envelope xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\" SOAP-ENV:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
			              "<SOAP-ENV:Body>" +
			              "<m:" + action + " xmlns:m=\"" + _serviceType + "\">";

			if (args != null)
			{
				foreach (var entry in args)
				{
					soap += "<" + entry.Key + ">" + entry.Value + "</" + entry.Key + ">";
				}
			}

			soap += "</m:" + action + "></SOAP-ENV:Body></SOAP-ENV:Envelope>";

			byte[] req = Encoding.ASCII.GetBytes(soap);

			HttpWebRequest request = (HttpWebRequest) WebRequest.Create(_controlURL);

			request.Method = "POST";
			request.ContentType = "text/xml";
			//request.Connection = "Close";
			request.ContentLength = req.Length;
			request.Headers.Add("SOAPAction", "\"" + _serviceType + "#" + action + "\"");
			Stream requestStream = request.GetRequestStream();
			requestStream.Write(req, 0, req.Length);
			requestStream.Close();

			HttpWebResponse response = (HttpWebResponse) request.GetResponse();
			if (response.StatusCode == HttpStatusCode.OK)
			{
				Stream responseStream = response.GetResponseStream();
				if (responseStream == null)
					throw new NullReferenceException("Null Response Stream");
				string responseStr = new StreamReader(responseStream).ReadToEnd();

				XDocument xml = XDocument.Parse(responseStr);
				XDocument doc = xml.Document;
				if (doc != null)
				{
					foreach (var node in doc.DescendantNodes())
					{
						try
						{
							XElement ele = node as XElement;
							if (ele == null)
								continue;

							if (ele.FirstNode.NodeType == XmlNodeType.Text)
							{
								XText txt = ele.FirstNode as XText;
								if (txt != null)
									ret[ele.Name.LocalName] = txt.Value;
							}
						}
						catch
						{
							// ignored
						}
					}
				}
				else
				{
					throw new NullReferenceException("XML Document is null");
				}
			}

			response.Close();

			return ret;
		}

		public string GetLocalIP()
		{
			return _interface.ToString();
		}

		public string GetExternalIP()
		{
			try
			{
				Dictionary<string, string> r = Command("GetExternalIPAddress");
				return r["NewExternalIPAddress"];
			}
			catch
			{
				return null;
			}
		}

		public bool OpenPort(int port, bool udp)
		{
			if (port < 0 || port > 65535)
				throw new ArgumentOutOfRangeException(nameof(port), "must be between 0-65535");

			Dictionary<string, string> args = new Dictionary<string, string>();
			args["NewRemoteHost"] = "";
			args["NewProtocol"] = udp ? "UDP" : "TCP";
			args["NewInternalClient"] = _interface.ToString();
			args["NewExternalPort"] = port.ToString();
			args["NewInternalPort"] = port.ToString();
			args["NewEnabled"] = "1";
			args["NewPortMappingDescription"] = "UPnPLib";
			args["NewLeaseDuration"] = "0";

			try
			{
				Dictionary<string, string> r = Command("AddPortMapping", args);
				return !r.ContainsKey("errorCode") || string.IsNullOrWhiteSpace(r["errorCode"]);
			}
			catch
			{
				return false;
			}
		}

		public bool ClosePort(int port, bool udp)
		{
			if (port < 0 || port > 65535)
				throw new ArgumentOutOfRangeException(nameof(port), "must be between 0-65535");

			Dictionary<string, string> args = new Dictionary<string, string>();

			args["NewRemoteHost"] = "";
			args["NewProtocol"] = udp ? "UDP" : "TCP";
			args["NewExternalPort"] = port.ToString();

			try
			{
				Command("DeletePortMapping", args);
				return true;
			}
			catch
			{
				return false;
			}
		}

		public bool IsMapped(int port, bool udp)
		{
			if (port < 0 || port > 65535)
				throw new ArgumentOutOfRangeException(nameof(port), "must be between 0-65535");

			Dictionary<string, string> args = new Dictionary<string, string>();

			args["NewRemoteHost"] = "";
			args["NewProtocol"] = udp ? "UDP" : "TCP";
			args["NewExternalPort"] = port.ToString();

			try
			{
				Dictionary<string, string> r = Command("GetSpecificPortMappingEntry", args);
				bool hasError = !r.ContainsKey("errorCode") || string.IsNullOrWhiteSpace(r["errorCode"]);
				if (hasError)
				{
					throw new Exception();
				}

				return r.ContainsKey("NewInternalPort") && r["NewInternalPort"] != null;
			}
			catch
			{
				return false;
			}
		}
	}
}
