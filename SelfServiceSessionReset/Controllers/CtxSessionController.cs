// Version 1.5
// Written by Jeremy Saunders (jeremy@jhouseconsulting.com) 13th June 2020
// Modified by Jeremy Saunders (jeremy@jhouseconsulting.com) 24th September 2020
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using SelfServiceSessionReset.Models;
// Required for collections of PowerShell Objects
using System.Collections.ObjectModel;
// Required for PowerSell
using System.Management.Automation;
// Required for PowerShell Runspaces
using System.Management.Automation.Runspaces;
// Required for threading
using System.Threading;
// Required for Expando objects
using System.Dynamic;
// Required for StringBuilder
using System.Text;
// Required for loading XML documents
using System.Xml.Linq;
// Required for both:
// HttpContext.Current.Server.MapPath
// HttpContext.Current.Request.LogonUserIdentity.Name
using System.Web;
// Required for File.ReadAllText
using System.IO;
// Required to lookup the users diplay name from Active Directory
using System.DirectoryServices.AccountManagement;
// Required for sockets XDPing
using System.Net.Sockets;

namespace SelfServiceSessionReset.Controllers
{
    /// <summary>
    /// This is the CtxSessionController class.
    /// </summary>
    [RoutePrefix("api/CtxSession")]
    public class CtxSessionController : ApiController
    {
        /// <summary>
        /// Get the logged in user identify name which is returned in the format of DOMAIN\\USERNAME
        /// </summary>
        /// <returns></returns>
        private string GetLoggedOnUser()
        {
            var userName = HttpContext.Current.Request.LogonUserIdentity.Name;
            return (userName);
        }

        /// <summary>
        /// Get user properties from Active Directory
        /// </summary>
        /// <returns></returns>
        private string GetADUserProperties()
        {
            // Get the current logged on username in the format of DOMAIN\\USERNAME
            string username = GetLoggedOnUser();
            // Split out the user Domain which is especially important when the user account is in a different domain to the app (IIS) server.
            string userdomain = username.Split('\\')[0];
            Thread.GetDomain().SetPrincipalPolicy(System.Security.Principal.PrincipalPolicy.WindowsPrincipal);
            System.Security.Principal.WindowsPrincipal principal = (System.Security.Principal.WindowsPrincipal)Thread.CurrentPrincipal;
            string displayName = string.Empty;
            string givenName = string.Empty;
            string surName = string.Empty;
            using (System.DirectoryServices.AccountManagement.PrincipalContext pc = new System.DirectoryServices.AccountManagement.PrincipalContext(System.DirectoryServices.AccountManagement.ContextType.Domain, userdomain))
            {
                System.DirectoryServices.AccountManagement.UserPrincipal up = System.DirectoryServices.AccountManagement.UserPrincipal.FindByIdentity(pc, username);
                if (up != null)
                {
                    givenName = up.GivenName;
                    surName = up.Surname;
                    displayName = up.DisplayName;
                }
            }
            return (givenName + " " + surName);
        }

        /// <summary>
        /// Performs an XDPing to make sure the Delivery Controller is in a healthy state
        /// </summary>
        /// <param name="deliverycontroller"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        private bool XDPing(string deliverycontroller, int port)
        {
            string service = "http://" + deliverycontroller + "/Citrix/CdsController/IRegistrar";
            string s = string.Format("POST {0} HTTP/1.1\r\nContent-Type: application/soap+xml; charset=utf-8\r\nHost: {1}:{2}\r\nContent-Length: 1\r\nExpect: 100-continue\r\nConnection: Close\r\n\r\n", (object)service, (object)deliverycontroller, (object)port);
            StringBuilder stringBuilder = new StringBuilder();
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            bool listening = false;
            try
            {
                socket.Connect(deliverycontroller, port);
                if (socket.Connected)
                {
                    stringBuilder.AppendLine("Socket connected");
                }
                byte[] bytes = Encoding.ASCII.GetBytes(s);
                socket.Send(bytes, bytes.Length, SocketFlags.None);
                byte[] numArray = new byte[21];
                socket.ReceiveTimeout = 5000;
                socket.Receive(numArray);
                string str = Encoding.ASCII.GetString(numArray);
                socket.Send(new byte[1] { (byte)32 }, 1, SocketFlags.None);
                socket.Close();
                if (str == "HTTP/1.1 100 Continue")
                {
                    listening = true;
                    stringBuilder.AppendLine("Service is listening");
                }
            }
            catch
            {
                stringBuilder.AppendLine("Failed to connect to service");
            }
            socket.Dispose();
            return listening;
        }

        /// <summary>
        /// Gets all current sessions for a user
        /// </summary>
        /// <param name="SiteName"></param>
        /// <param name="AdminAddress"></param>
        /// <param name="UserName"></param>
        /// <returns></returns>
        private List<CtxSession> GetCurrentSessions(string SiteName, string AdminAddress, string UserName)
        {
            StringBuilder stringBuilder = new StringBuilder();
            List<CtxSession> sessions = new List<CtxSession> { };

            Runspace runSpace = RunspaceFactory.CreateRunspace();
            runSpace.Open();
            PowerShell ps = PowerShell.Create();
            ps.Runspace = runSpace;
            PSSnapInException psex;
            runSpace.RunspaceConfiguration.AddPSSnapIn("Citrix.Broker.Admin.V2", out psex);
            Pipeline pipeline = runSpace.CreatePipeline();
            Command getSession = new Command("Get-BrokerSession");
            getSession.Parameters.Add("AdminAddress", AdminAddress);
            getSession.Parameters.Add("MaxRecordCount", 99999);
            getSession.Parameters.Add("UserName", UserName);
            pipeline.Commands.Add(getSession);
            try
            {
                Collection<PSObject> Output = pipeline.Invoke();
                runSpace.Close();

                // Check if the collection is null or empty
                bool IsCollectionNullOrEmpty = !(Output?.Any() ?? false);

                if (IsCollectionNullOrEmpty == false)
                {
                    foreach (PSObject obj in Output)
                    {
                        string MachineName = string.Empty;
                        string HostedMachineName = string.Empty;
                        string SessionState = string.Empty;
                        string StartTime = string.Empty;
                        string SessionStateChangeTime = string.Empty;
                        string OSType = string.Empty;
                        string SessionSupport = string.Empty;
                        string SessionId = string.Empty;
                        string ApplicationState = string.Empty;
                        string ApplicationStateLastChangeTime = string.Empty;
                        string[] ApplicationsInUse = new string[] { };
                        string IdleDuration = string.Empty;
                        string IdleSince = string.Empty;
                        string RestartSupported = string.Empty;
                        string Hidden = string.Empty;
                        string PowerState = string.Empty;
                        string RegistrationState = string.Empty;
                        string MaintenanceMode = string.Empty;
                        bool Include = false;

                        if (!(obj is null))
                        {
                            MachineName = obj.Properties["MachineName"].Value.ToString();
                            if (obj.Properties["HostedMachineName"].Value != null)
                            {
                                HostedMachineName = obj.Properties["HostedMachineName"].Value.ToString();
                            }
                            SessionState = obj.Properties["SessionState"].Value.ToString();
                            if (obj.Properties["StartTime"].Value != null)
                            {
                                StartTime = obj.Properties["StartTime"].Value.ToString();
                            }
                            SessionStateChangeTime = obj.Properties["SessionStateChangeTime"].Value.ToString();
                            OSType = obj.Properties["OSType"].Value.ToString();
                            SessionSupport = obj.Properties["SessionSupport"].Value.ToString();
                            SessionId = obj.Properties["SessionId"].Value.ToString();
                            ApplicationState = obj.Properties["AppState"].Value.ToString();
                            if (obj.Properties["AppState"].Value.ToString() == "NoApps")
                            {
                                ApplicationState = "Application not running";
                            }
                            ApplicationStateLastChangeTime = obj.Properties["AppStateLastChangeTime"].Value.ToString();
                            // The ApplicationsInUse value is a System.String[] which is an array that can have a lower bound other than zero.
                            // They are incompatible with a regular string[] array, so we for need to cast it to a System.Array and work with it that way.
                            // Then we use LINQ (System.Linq namespace) to convert the System.Array to string[] and we get the desired outcome.
                            Array arrApplications = (Array)obj.Properties["ApplicationsInUse"].Value;
                            ApplicationsInUse = arrApplications.OfType<object>().Select(o => o.ToString()).ToArray();
                            if (obj.Properties["IdleDuration"].Value != null)
                            {
                                IdleDuration = obj.Properties["IdleDuration"].Value.ToString();
                            }
                            if (obj.Properties["IdleSince"].Value != null)
                            {
                                IdleSince = obj.Properties["IdleSince"].Value.ToString();
                            }
                            RestartSupported = "False";
                            if (OSType.IndexOf("Windows", StringComparison.CurrentCultureIgnoreCase) >= 0 && OSType.IndexOf("20", StringComparison.CurrentCultureIgnoreCase) < 0 && SessionSupport == "SingleSession")
                            {
                                RestartSupported = "True";
                            }
                            Hidden = obj.Properties["Hidden"].Value.ToString();
                            PowerState = obj.Properties["PowerState"].Value.ToString();

                            // Include or Exclude specific delivery groups
                            Include = IncludeDeliveryGroup(SiteName, obj.Properties["DesktopGroupName"].Value.ToString());

                            // Retrieve an object of extra information of the session, such as RegistrationState of the machine.
                            dynamic extraSessionInfo = GetSessionExtraInformation(AdminAddress, MachineName);

                            sessions.Add(new CtxSession
                            {
                                MachineName = MachineName,
                                HostedMachineName = HostedMachineName,
                                SessionState = SessionState,
                                StartTime = StartTime,
                                SessionStateChangeTime = SessionStateChangeTime,
                                OSType = OSType,
                                SessionSupport = SessionSupport,
                                SessionId = SessionId,
                                ApplicationState = ApplicationState,
                                ApplicationStateLastChangeTime = ApplicationStateLastChangeTime,
                                ApplicationsInUse = ApplicationsInUse,
                                IdleDuration = IdleDuration,
                                IdleSince = IdleSince,
                                RestartSupported = RestartSupported,
                                Hidden = Hidden,
                                PowerState = PowerState,
                                RegistrationState = extraSessionInfo.RegistrationState,
                                MaintenanceMode = extraSessionInfo.MaintenanceMode,
                                Include = Include
                            });
                        }
                    };
                }
            }
            catch (Exception e)
            {
                stringBuilder.AppendLine("ERROR: " + e.Message);
            }
            return sessions;
        }

        /// <summary>
        /// Returns an Expando object with the RegistrationState of a machine
        /// </summary>
        /// <param name="AdminAddress"></param>
        /// <param name="MachineName"></param>
        /// <returns></returns>
        private ExpandoObject GetSessionExtraInformation(string AdminAddress, string MachineName)
        {
            StringBuilder stringBuilder = new StringBuilder();
            // Create a dynamic object to store some properties
            dynamic response = new ExpandoObject();
            response.RegistrationState = string.Empty;
            response.MaintenanceMode = string.Empty;

            Runspace runSpace = RunspaceFactory.CreateRunspace();
            runSpace.Open();
            PowerShell ps = PowerShell.Create();
            ps.Runspace = runSpace;
            PSSnapInException psex;
            runSpace.RunspaceConfiguration.AddPSSnapIn("Citrix.Broker.Admin.V2", out psex);
            Pipeline pipeline = runSpace.CreatePipeline();
            Command getMachine = new Command("Get-BrokerMachine");
            getMachine.Parameters.Add("AdminAddress", AdminAddress);
            getMachine.Parameters.Add("MachineName", MachineName);
            pipeline.Commands.Add(getMachine);
            try
            {
                Collection<PSObject> Output = pipeline.Invoke();
                runSpace.Close();

                // Check if the collection is null or empty
                bool IsCollectionNullOrEmpty = !(Output?.Any() ?? false);

                if (IsCollectionNullOrEmpty == false)
                {
                    foreach (PSObject obj in Output)
                    {

                        if (!(obj is null))
                        {
                            response.RegistrationState = obj.Properties["RegistrationState"].Value.ToString();
                            if (obj.Properties["InMaintenanceMode"].Value.ToString().ToLower() == "false")
                            {
                                response.MaintenanceMode = "Off";
                            }
                            else if (obj.Properties["InMaintenanceMode"].Value.ToString().ToLower() == "true")
                            {
                                response.MaintenanceMode = "On";
                            }
                        }
                    };
                }
            }
            catch (Exception e)
            {
                stringBuilder.AppendLine("ERROR: " + e.Message);
            }
            return response;
        }

        /// <summary>
        /// Check if the Delivery Group should be included by assessing the IncludeDeliveryGroups and
        /// ExcludeDeliveryGroups elements from the Site Name node of the App_Data/CtxSites.xml file.
        /// (1) If IncludeDeliveryGroups is empty, all Delivery Groups will be included by default.
        /// (2) If ExcludeDeliveryGroups is empty, no Delivery Groups will be excluded by default unless
        /// IncludeDeliveryGroups is set.
        /// (3) ExcludeDeliveryGroups will override IncludeDeliveryGroups
        /// (4) IncludeDeliveryGroup and ExcludeDeliveryGroups can contain a partial name of a Delivery
        /// Group. So if you have a standard naming convention, you can include all Delivery Groups that
        /// contain a string pattern in their name.
        /// </summary>
        /// <param name="SiteName"></param>
        /// <param name="DeliveryGroup"></param>
        /// <returns></returns>
        private bool IncludeDeliveryGroup(string SiteName, string DeliveryGroup)
        {
            bool leave = false;
            bool include = false;
            StringBuilder stringBuilder = new StringBuilder();

            if (File.Exists(HttpContext.Current.Server.MapPath("~/App_Data/CtxSites.xml")))
            {
                stringBuilder.AppendLine("Found the CtxSites.xml file");
                XDocument doc = XDocument.Load(HttpContext.Current.Server.MapPath("~/App_Data/CtxSites.xml"));
                foreach (XElement element in doc.Descendants("Sites").Descendants("Site"))
                {
                    if (element.Element("Name").Value.ToUpper().Equals(SiteName.ToUpper()))
                    {
                        leave = true;
                        stringBuilder.AppendLine(SiteName + " site found");
                        if (string.IsNullOrEmpty(element.Element("IncludeDeliveryGroups").Value))
                        {
                            stringBuilder.AppendLine("IncludeDeliveryGroups element is empty");
                            include = true;
                        }
                        else
                        {
                            stringBuilder.AppendLine("IncludeDeliveryGroups element is not empty");
                            string[] strArray = element.Element("IncludeDeliveryGroups").Value.Split(',');
                            foreach (string strItem in strArray)
                            {
                                stringBuilder.AppendLine("Processing " + strItem + " Delivery Group");
                                if (DeliveryGroup.IndexOf(strItem, StringComparison.CurrentCultureIgnoreCase) >= 0)
                                {
                                    stringBuilder.AppendLine("Inclusion Match");
                                    include = true;
                                    break;
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(element.Element("ExcludeDeliveryGroups").Value))
                        {
                            stringBuilder.AppendLine("ExcludeDeliveryGroups element is not empty");
                            string[] strArray = element.Element("ExcludeDeliveryGroups").Value.Split(',');
                            foreach (string strItem in strArray)
                            {
                                stringBuilder.AppendLine("Processing " + strItem + " Delivery Group");
                                if (DeliveryGroup.IndexOf(strItem, StringComparison.CurrentCultureIgnoreCase) >= 0)
                                {
                                    stringBuilder.AppendLine("Exclusion Match");
                                    include = false;
                                    break;
                                }
                            }
                        }
                        if (leave) { break; }
                    }
                    else
                    {
                        stringBuilder.AppendLine("ERROR: Site not found");
                    }
                }
            }
            stringBuilder.ToString();
            return include;
        }

        /// <summary>
        /// Logs off all sepecified sessions.
        /// </summary>
        /// <param name="AdminAddress"></param>
        /// <param name="UserName"></param>
        /// <param name="arrMachineNames"></param>
        /// <returns></returns>
        private string LogoffSessions(string AdminAddress, string UserName, string[] arrMachineNames)
        {
            Runspace runSpace = RunspaceFactory.CreateRunspace();
            runSpace.Open();
            PowerShell ps = PowerShell.Create();
            ps.Runspace = runSpace;
            PSSnapInException psex;
            runSpace.RunspaceConfiguration.AddPSSnapIn("Citrix.Broker.Admin.V2", out psex);
            StringBuilder stringBuilder = new StringBuilder();
            foreach (string machinename in arrMachineNames)
            {
                stringBuilder.AppendLine("Logging off " + machinename);
                Pipeline pipeline = runSpace.CreatePipeline();
                Command getSession = new Command("Get-BrokerSession");
                getSession.Parameters.Add("AdminAddress", AdminAddress);
                getSession.Parameters.Add("MachineName", "*\\" + machinename);
                getSession.Parameters.Add("UserName", UserName);
                pipeline.Commands.Add(getSession);
                Command stopSession = new Command("Stop-BrokerSession");
                pipeline.Commands.Add(stopSession);
                try
                {
                    Collection<PSObject> commandResults = pipeline.Invoke();
                    bool IsCollectionNullOrEmpty = !(commandResults?.Any() ?? false);
                    if (IsCollectionNullOrEmpty == false)
                    {
                        foreach (PSObject obj in commandResults)
                        {
                            if (!(obj is null))
                            {
                                stringBuilder.AppendLine(obj.ToString());
                            }
                            else
                            {
                                stringBuilder.AppendLine("Failed to log off " + machinename);
                            }
                        };
                    }
                    else
                    {
                        stringBuilder.AppendLine("Successfully logged off " + machinename);
                    }
                    pipeline.Dispose();
                }
                catch (Exception e)
                {
                    stringBuilder.AppendLine("ERROR: " + e.Message);
                }
            }
            runSpace.Close();
            // Unfortunately the Stop-BrokerSession cmdlet doesn't return anything
            // So we do our best to return meaninful data.
            return stringBuilder.ToString();
        }

        /// <summary>
        /// Restarts all sepecified machines.
        /// </summary>
        /// <param name="AdminAddress"></param>
        /// <param name="arrMachineNames"></param>
        /// <param name="reset"></param>
        /// <returns></returns>
        private string RestartMachines(string AdminAddress, string[] arrMachineNames, bool reset)
        {
            Runspace runSpace = RunspaceFactory.CreateRunspace();
            runSpace.Open();
            PowerShell ps = PowerShell.Create();
            ps.Runspace = runSpace;
            PSSnapInException psex;
            runSpace.RunspaceConfiguration.AddPSSnapIn("Citrix.Broker.Admin.V2", out psex);
            StringBuilder stringBuilder = new StringBuilder();
            foreach (string machinename in arrMachineNames)
            {
                stringBuilder.AppendLine("Restarting " + machinename);
                Pipeline pipeline = runSpace.CreatePipeline();
                Command getSession = new Command("New-BrokerHostingPowerAction");
                getSession.Parameters.Add("AdminAddress", AdminAddress);
                getSession.Parameters.Add("MachineName", machinename);
                if (reset == false)
                {
                    getSession.Parameters.Add("Action", "Restart");
                }
                else
                {
                    getSession.Parameters.Add("Action", "Reset");
                }
                pipeline.Commands.Add(getSession);
                try
                {
                    Collection<PSObject> commandResults = pipeline.Invoke();
                    bool IsCollectionNullOrEmpty = !(commandResults?.Any() ?? false);
                    if (IsCollectionNullOrEmpty == false)
                    {
                        foreach (PSObject obj in commandResults)
                        {
                            if (!(obj is null))
                            {
                                //stringBuilder.AppendLine(obj.ToString());
                                stringBuilder.AppendLine("Restart state: " + obj.Properties["State"].Value.ToString());

                            }
                            else
                            {
                                stringBuilder.AppendLine("Failed to restart " + machinename);
                            }
                        };
                    }
                    else
                    {
                        stringBuilder.AppendLine("No output returned when restarting " + machinename + ". Outcome unknown.");
                    }
                    pipeline.Dispose();
                }
                catch (Exception e)
                {
                    stringBuilder.AppendLine("ERROR: " + e.Message);
                }
            }
            runSpace.Close();
            return stringBuilder.ToString();
        }

        /// <summary>
        /// Hides all sepecified sessions.
        /// </summary>
        /// <param name="AdminAddress"></param>
        /// <param name="UserName"></param>
        /// <param name="arrMachineNames"></param>
        /// <param name="hide"></param>
        /// <returns></returns>
        private string HideSessions(string AdminAddress, string UserName, string[] arrMachineNames, bool hide)
        {
            Runspace runSpace = RunspaceFactory.CreateRunspace();
            runSpace.Open();
            PowerShell ps = PowerShell.Create();
            ps.Runspace = runSpace;
            PSSnapInException psex;
            runSpace.RunspaceConfiguration.AddPSSnapIn("Citrix.Broker.Admin.V2", out psex);
            StringBuilder stringBuilder = new StringBuilder();
            foreach (string machinename in arrMachineNames)
            {
                stringBuilder.AppendLine("Hiding session on " + machinename);
                Pipeline pipeline = runSpace.CreatePipeline();
                Command getSession = new Command("Get-BrokerSession");
                getSession.Parameters.Add("AdminAddress", AdminAddress);
                getSession.Parameters.Add("MachineName", "*\\" + machinename);
                getSession.Parameters.Add("UserName", UserName);
                pipeline.Commands.Add(getSession);
                Command setSession = new Command("Set-BrokerSession");
                setSession.Parameters.Add("hidden", hide);
                pipeline.Commands.Add(setSession);
                try
                {
                    Collection<PSObject> commandResults = pipeline.Invoke();
                    bool IsCollectionNullOrEmpty = !(commandResults?.Any() ?? false);
                    if (IsCollectionNullOrEmpty == false)
                    {
                        foreach (PSObject obj in commandResults)
                        {
                            if (!(obj is null))
                            {
                                stringBuilder.AppendLine(obj.ToString());
                            }
                            else
                            {
                                stringBuilder.AppendLine("Failed to hide session on " + machinename);
                            }
                        };
                    }
                    else
                    {
                        stringBuilder.AppendLine("Successfully hidden session on " + machinename);
                    }
                    pipeline.Dispose();
                }
                catch (Exception e)
                {
                    stringBuilder.AppendLine("ERROR: " + e.Message + Environment.NewLine);
                }
            }
            runSpace.Close();
            // Unfortunately the Set-BrokerSession cmdlet doesn't return anything
            // So we do our best to return meaninful data.
            return stringBuilder.ToString();
        }

        /// <summary>
        /// Gets all site data from the App_Data/CtxSites.xml file.
        /// </summary>
        /// <param name="usexml"></param>
        /// <param name="usejson"></param>
        /// <returns></returns>
        private List<CtxSites> GetAllSites(bool usexml, bool usejson)
        {
            List<CtxSites> sites = new List<CtxSites>();
            if (usexml)
            {
                if (File.Exists(HttpContext.Current.Server.MapPath("~/App_Data/CtxSites.xml")))
                {
                    XDocument doc = XDocument.Load(HttpContext.Current.Server.MapPath("~/App_Data/CtxSites.xml"));
                    foreach (XElement element in doc.Descendants("Sites").Descendants("Site"))
                    {
                        CtxSites site = new CtxSites();
                        site.FriendlyName = element.Element("FriendlyName").Value;
                        site.Name = element.Element("Name").Value;
                        site.DeliveryControllers = element.Element("DeliveryControllers").Value;
                        site.Port = element.Element("Port").Value;
                        site.Default = element.Element("Default").Value;
                        sites.Add(site);
                    }
                }
            }
            return sites;
        }

        // GET: api/CtxSession/GetSiteList
        /// <summary>
        /// This method returns an array of JSON objects read from the App_Data\CtxSites.xml file.
        /// Each Site contains 4 elements.
        /// (1) FriendlyName - the name in the drop down list that Users will associate with.
        /// (2) Name - the real Site name.
        /// (3) DeliveryControllers - a comma separated list of Delivery Controllers for that site.
        /// (4) Port – the port number the Delivery Controllers listen on. An XDPing is run against them to ensure they are healthy.
        /// (5) Default - the default value is False. Set this to True if you have multiple Sites but want 1 Site to be the default
        /// selected when users access the tool. If only 1 Site is added to the XML file, it will automatically default to this Site
        /// so there is no need to set a value for Default.
        /// </summary>
        /// <returns>List of sites</returns>
        [Route("GetSiteList")]
        [HttpGet]
        public IHttpActionResult GetSiteList()
        {
            bool usexml = true;
            bool usejson = false;
            return Ok(GetAllSites(usexml, usejson));
        }

        // GET: api/CtxSession/GetLoggedOnUserName
        /// <summary>
        /// This method returns the logged on username in the format of DOMAIN\USERNAME.
        /// </summary>
        /// <returns>Username</returns>
        [Route("GetLoggedOnUserName")]
        [HttpGet]
        public IHttpActionResult GetLoggedOnUserName()
        {
            return Ok(GetLoggedOnUser());
        }

        // GET: api/CtxSession/GetDisplayName
        /// <summary>
        /// This method returns the users Display Name in the format of "firstname surname" which it reads from Active Directory.
        /// </summary>
        /// <returns>Display Name</returns>
        [Route("GetDisplayName")]
        [HttpGet]
        public IHttpActionResult GetDisplayName()
        {
            return Ok(GetADUserProperties());
        }

        // GET: api/CtxSession/GetSessions?sitename=&deliverycontrollers=&port=
        /// <summary>
        /// This method returns all sessions for the user.
        /// </summary>
        /// <param name="sitename"></param>
        /// <param name="deliverycontrollers"></param>
        /// <param name="port"></param>
        /// <returns>All sessions for a user</returns>
        [Route("GetSessions")]
        [HttpGet]
        public IEnumerable<CtxSession> GetSessions(string sitename, string deliverycontrollers, string port)
        {
            int.TryParse(port, out int intport);
            string deliverycontroller = string.Empty;
            string[] strArray = deliverycontrollers.Split(',');
            foreach (string strItem in strArray)
            {
                deliverycontroller = strItem;
                if (XDPing(deliverycontroller, intport))
                {
                    break;
                }
            }
            string username = GetLoggedOnUser();
            return GetCurrentSessions(sitename, deliverycontroller, username);
        }

        // GET: api/CtxSession/GetSession?machinename=&sitename=&deliverycontrollers=&port=
        /// <summary>
        /// This method returns a session for the user based on the machine name.
        /// </summary>
        /// <param name="machinename"></param>
        /// <param name="sitename"></param>
        /// <param name="deliverycontrollers"></param>
        /// <param name="port"></param>
        /// <returns>A session for a user</returns>
        [Route("GetSession")]
        [HttpGet]
        public IEnumerable<CtxSession> GetSession(string machinename, string sitename, string deliverycontrollers, string port)
        {
            int.TryParse(port, out int intport);
            string deliverycontroller = string.Empty;
            string[] strArray = deliverycontrollers.Split(',');
            foreach (string strItem in strArray)
            {
                deliverycontroller = strItem;
                if (XDPing(deliverycontroller, intport))
                {
                    break;
                }
            }
            string username = GetLoggedOnUser();
            CtxSession[] sessionArray = GetCurrentSessions(sitename, deliverycontroller, username).Where<CtxSession>(c => c.MachineName.Contains(machinename)).ToArray<CtxSession>();
            return sessionArray;
        }

        // DELETE: api/CtxSession/LogoffSessions
        /// <summary>
        /// This method logs off specified sessions.
        /// The Delivery Controllers, Port, and an array of sessions to logoff are passed in the body using JSON format.
        /// </summary>
        /// <param name="logoffinfo"></param>
        /// <returns>Success or failure</returns>
        [Route("LogoffSessions")]
        [HttpDelete]
        public IHttpActionResult LogoffSessionsByMachineName([FromBody]CtxSessionsToAction logoffinfo)
        {
            int.TryParse(logoffinfo.Port, out int intport);
            string deliverycontroller = string.Empty;
            string[] strArray = logoffinfo.DeliveryControllers.Split(',');
            foreach (string strItem in strArray)
            {
                deliverycontroller = strItem;
                if (XDPing(deliverycontroller, intport))
                {
                    break;
                }
            }
            string username = GetLoggedOnUser();
            string[] machinearray = logoffinfo.MachineNames.ToArray();
            string result = LogoffSessions(deliverycontroller, username, machinearray);
            return Ok(result);
        }

        // DELETE: api/CtxSession/RestartMachines
        /// <summary>
        /// This method restarts specified machines.
        /// The Site Name, Delivery Controllers, Port, and an array of sessions to restart are passed in the body using JSON format.
        /// Supported on Windows Desktop machines only.
        /// </summary>
        /// <param name="restartinfo"></param>
        /// <returns>Success or failure</returns>
        [Route("RestartMachines")]
        [HttpDelete]
        public IHttpActionResult RestartMachinesByMachineName([FromBody]CtxSessionsToAction restartinfo)
        {
            int.TryParse(restartinfo.Port, out int intport);
            bool reset = restartinfo.Reset;
            string sitename = restartinfo.SiteName;
            string deliverycontroller = string.Empty;
            string[] strArray = restartinfo.DeliveryControllers.Split(',');
            foreach (string strItem in strArray)
            {
                deliverycontroller = strItem;
                if (XDPing(deliverycontroller, intport))
                {
                    break;
                }
            }
            string username = GetLoggedOnUser();
            string[] machinearray = restartinfo.MachineNames.ToArray();
            string result = string.Empty;
            // Create a new array by verifying that each machine meets the following criteria.
            // - OSType contains Windows
            // AND
            // - OSType does not contain 20 for Windows 2008 R2, 2012 R2, 2016, 2019, etc
            // AND
            // - SessionSupport must be SingleSession
            List<string> CriteriaMetList = new List<string>();
            foreach (string machinename in machinearray)
            {
                CtxSession[] sessionArray = GetCurrentSessions(sitename, deliverycontroller, username).Where<CtxSession>(c => c.MachineName.Contains(machinename) && c.OSType.Contains("Windows") && !c.OSType.Contains("20") && c.SessionSupport == "SingleSession").ToArray<CtxSession>();
                if (sessionArray.Length == 1)
                {
                    CriteriaMetList.Add(machinename);
                    result += machinename + " meets the criteria to be restarted" + Environment.NewLine;

                }
                else if (sessionArray.Length == 0)
                {
                    result += machinename + " does not meet the criteria to be restarted" + Environment.NewLine;
                }
            }
            result += RestartMachines(deliverycontroller, CriteriaMetList.ToArray(), reset);
            return Ok(result);
        }

        // PUT: api/CtxSession/HideSessions
        /// <summary>
        /// This method hides specified sessions.
        /// The Site Name, Delivery Controllers, Port, and an array of sessions to hide are passed in the body using JSON format.
        /// Each machine must meet the following criteria, which prevents users from hiding sessions unnecessarily.
        /// Registration State must be Unregistered
        /// OR
        /// Power State must be Unknown or Off
        /// OR
        /// Maintenance Mode must be On.
        /// </summary>
        /// <param name="hideinfo"></param>
        /// <returns>Success or failure</returns>
        [Route("HideSessions")]
        [HttpPut]
        public IHttpActionResult HideSessionsByMachineName([FromBody]CtxSessionsToAction hideinfo)
        {
            int.TryParse(hideinfo.Port, out int intport);
            bool hide = true;
            string sitename = hideinfo.SiteName;
            string deliverycontroller = string.Empty;
            string[] strArray = hideinfo.DeliveryControllers.Split(',');
            foreach (string strItem in strArray)
            {
                deliverycontroller = strItem;
                if (XDPing(deliverycontroller, intport))
                {
                    break;
                }
            }
            string username = GetLoggedOnUser();
            string[] machinearray = hideinfo.MachineNames.ToArray();
            string result = string.Empty;
            // Create a new array by verifying that each machine meets the following criteria, which prevents users from hiding sessions unnecessarily. 
            // - RegistrationState must be Unregistered
            // OR
            // - PowerState must be Unknown or Off
            // OR
            // - MaintenanceMode must be On
            List<string> CriteriaMetList = new List<string>();
            foreach (string machinename in machinearray)
            {
                CtxSession[] sessionArray = GetCurrentSessions(sitename, deliverycontroller, username).Where<CtxSession>(c => c.MachineName.Contains(machinename) && (c.RegistrationState == "Unregistered" || c.PowerState == "Unknown" || c.PowerState == "Off" || c.MaintenanceMode == "On")).ToArray<CtxSession>();
                if (sessionArray.Length == 1)
                {
                    CriteriaMetList.Add(machinename);
                    result += machinename + " meets the criteria to be hidden" + Environment.NewLine;

                }
                else if (sessionArray.Length == 0)
                {
                    result += machinename + " does not meet the criteria to be hidden" + Environment.NewLine;
                }
            }
            result += HideSessions(deliverycontroller, username, CriteriaMetList.ToArray(), hide);
            return Ok(result);
        }

    }
}
