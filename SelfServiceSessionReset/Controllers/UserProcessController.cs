// Version 1.3
// Written by Jeremy Saunders (jeremy@jhouseconsulting.com) 13th June 2020
// Modified by Jeremy Saunders (jeremy@jhouseconsulting.com) 25th July 2021
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using SelfServiceSessionReset.Models;
// Required namespaces for managing processes
using System.Diagnostics;
using System.Management;
// Required for Expando objects
using System.Dynamic;
// Required for threading
using System.Threading;
// Required for StringBuilder
using System.Text;
// Required for both:
// HttpContext.Current.Server.MapPath
// HttpContext.Current.Request.LogonUserIdentity.Name
using System.Web;
// Required to read appSettings from web.config
using System.Configuration;
// Required for using a NameValueCollection
using System.Collections.Specialized;
// Required for logging
using Serilog;

namespace SelfServiceSessionReset.Controllers
{

    /// <summary>
    /// This is the UserProcessController class.
    /// </summary>
    [RoutePrefix("api/UserProcess")]
    public class UserProcessController : ApiController
    {
        /// <summary>
        /// A private class for the key value pair used by the GetConfigurationSettings method
        /// </summary>
        private class ConfigSettings
        {
            public string key { get; set; }
            public string value { get; set; }
        }

        /// <summary>
        /// Get the configuration information from appSettings in the Web.config, skipping the serilog settings
        /// </summary>
        private List<ConfigSettings> GetConfigurationSettings()
        {
            List<ConfigSettings> ConfigurationSettings = new List<ConfigSettings> { };
            NameValueCollection appSettings = ConfigurationManager.AppSettings;

            foreach (string s in appSettings.AllKeys)
            {
                if (s.IndexOf("serilog", StringComparison.CurrentCultureIgnoreCase) < 0)
                {
                    ConfigurationSettings.Add(new ConfigSettings
                    {
                        key = s,
                        value = appSettings.Get(s)
                    });
                }
            }
            return ConfigurationSettings;
        }

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
        /// This method returns all the processes.
        /// </summary>
        /// <param name="remotehost"></param>
        /// <param name="username"></param>
        /// <returns></returns>
        private List<UserProcess> GetRemoteUserProcesses(string remotehost, string username)
        {
            string result = string.Empty;
            List<UserProcess> userprocesses = new List<UserProcess> { };

            try
            {
                // Create an array to store the processes
                Process[] processList = Process.GetProcesses(remotehost);

                // Loop through the array of processes to show information of every process in your console
                foreach (Process process in processList)
                {
                    // Retrieve an object of extra information of the process, such as Username, Description and CommandLine
                    dynamic extraProcessInfo = GetProcessExtraInformation(process.Id, remotehost);

                    if (extraProcessInfo.Username.ToLower() == username.ToLower())
                    {
                        // Retrieve an object of extra information of the process, such as CPU Usage
                        dynamic extraPerformanceInfo = GetPerfMonCounterInformation(process.ProcessName, process.Id, remotehost);

                        string bytesPrivateMemorySize = BytesToReadableValue(process.PrivateMemorySize64);
                        string bytesWorkingSet = BytesToReadableValue(process.WorkingSet64);
                        string bytesPeakWorkingSet = BytesToReadableValue(process.PeakWorkingSet64);
                        string bytesPagedMemorySize = BytesToReadableValue(process.PagedMemorySize64);

                        userprocesses.Add(new UserProcess
                        {
                            // 1 Process name
                            Name = process.ProcessName,
                            // 2 Process ID
                            PID = process.Id.ToString(),
                            // 3 Memory usage - PrivateMemorySize
                            BytesPrivateMemorySize = bytesPrivateMemorySize,
                            // 4 Memory usage - WorkingSet
                            BytesWorkingSet = bytesWorkingSet,
                            // 5 Memory usage - PeakWorkingSet
                            BytesPeakWorkingSet = bytesPeakWorkingSet,
                            // 6 Memory usage - PagedMemorySize (AKA Commit Size)
                            BytesPagedMemorySize = bytesPagedMemorySize,
                            // 7 CPU usage percent
                            CpuUsagePercentage = extraPerformanceInfo.CpuUsagePercentage,
                            // 8 Handle Count
                            HandleCount = process.HandleCount.ToString(),
                            // 9 Threads
                            Threads = process.Threads.Count.ToString(),
                            // 10 Command line of the process
                            CommandLine = extraProcessInfo.CommandLine,
                            // 11 Description of the process
                            Description = extraProcessInfo.Description
                        });
                    }
                }
            }
            catch (Exception e)
            {
                result = "Exception occured in an attempt to connecct to " + remotehost + " to get all processes for " + username + ": " + e.Message;
            }
            return userprocesses;
        }

        /// <summary>
        /// Returns an Expando object with the description, commandline and username of a process from the process ID.
        /// </summary>
        /// <param name="processId"></param>
        /// <param name="remotehost"></param>
        /// <returns></returns>
        private ExpandoObject GetProcessExtraInformation(int processId, string remotehost)
        {
            ConnectionOptions connOptions = new ConnectionOptions();
            connOptions.Impersonation = ImpersonationLevel.Impersonate;
            connOptions.EnablePrivileges = true;
            ManagementScope manScope = new ManagementScope(String.Format(@"\\{0}\ROOT\CIMV2", remotehost), connOptions);
            manScope.Connect();

            // Query the Win32_Process
            SelectQuery query = new SelectQuery("Select * from Win32_Process Where ProcessId = " + processId);
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(manScope, query);
            ManagementObjectCollection processList = searcher.Get();

            // Create a dynamic object to store some properties
            dynamic response = new ExpandoObject();
            response.Description = "";
            response.CommandLine = "";
            response.Username = "Unknown";

            foreach (ManagementObject obj in processList)
            {
                // Retrieve username 
                string[] argList = new string[] { string.Empty, string.Empty };
                int returnVal = Convert.ToInt32(obj.InvokeMethod("GetOwner", argList));
                if (returnVal == 0)
                {
                    // You can return the domain with the username like MYDOMAIN\Username
                    response.Username = argList[1] + "\\" + argList[0];
                    // Or just return the username
                    //response.Username = argList[0];

                }

                // Retrieve process description if exists
                if (obj["ExecutablePath"] != null)
                {
                    try
                    {
                        FileVersionInfo info = FileVersionInfo.GetVersionInfo(obj["ExecutablePath"].ToString());
                        response.Description = info.FileDescription;
                    }
                    catch { }
                }
                // Retrieve commandline
                if (obj["CommandLine"] != null)
                {
                    try
                    {
                        response.CommandLine = obj["CommandLine"].ToString();
                    }
                    catch
                    {
                        //
                    }
                }
            }
            return response;
        }

        /// <summary>
        /// Method that converts bytes to its human readable value.
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        private string BytesToReadableValue(long number)
        {
            List<string> suffixes = new List<string> { " B", " KB", " MB", " GB", " TB", " PB" };

            for (int i = 0; i < suffixes.Count; i++)
            {
                long temp = number / (int)Math.Pow(1024, i + 1);

                if (temp == 0)
                {
                    return (number / (int)Math.Pow(1024, i)) + suffixes[i];
                }
            }

            return number.ToString();
        }

        /// <summary>
        /// Get extra information in the form of performance counter values.
        /// </summary>
        /// <param name="processName"></param>
        /// <param name="processId"></param>
        /// <param name="remotehost"></param>
        /// <returns></returns>
        private ExpandoObject GetPerfMonCounterInformation(string processName, int processId, string remotehost)
        {
            string result = "";
            // Get the number of logical processors from the remote host
            int logicalprocessors = GetLogicalProcessors(remotehost);
            string processCounterName = "% Processor Time";

            // Create a dynamic object to store some properties
            dynamic response = new ExpandoObject();
            response.CpuUsagePercentage = 0;

            var counter = GetPerfCounterForProcessId(processName, processId, processCounterName, remotehost);

            if (counter != null)
            {
                // Start capturing
                // The first call will always return 0
                counter.NextValue();
                // That's why we need to sleep 1 second. Therefore we are essentially measuring a 1 second data capture.
                Thread.Sleep(1000);
                // The second call determines, the % of time that the monitored process uses on % User time for a single processor.
                // So the limit is 100% * the number of processors you have.
                // Hence we need to divide by the number of logical processors to get the average CPU usage of one process during the time measured.
                // Rounding up the int value to two decimal places becomes a double.
                response.CpuUsagePercentage = Math.Round(counter.NextValue() / logicalprocessors, 2);
                result = counter.InstanceName + " -  Cpu: " + response.CpuUsagePercentage;
            }

            return response;
        }

        /// <summary>
        /// This method gets the performance counter.
        /// </summary>
        /// <param name="processName"></param>
        /// <param name="processId"></param>
        /// <param name="processCounterName"></param>
        /// <param name="remotehost"></param>
        /// <returns></returns>
        private PerformanceCounter GetPerfCounterForProcessId(string processName, int processId, string processCounterName, string remotehost)
        {
            // Find the process counter instance by process ID
            string instance = GetInstanceNameForProcessId(processName, processId, remotehost);
            if (string.IsNullOrEmpty(instance))
                return null;

            return new PerformanceCounter("Process", processCounterName, instance, remotehost);
        }

        /// <summary>
        /// This method finds the process counter instance by process ID.
        /// </summary>
        /// <param name="processName"></param>
        /// <param name="processId"></param>
        /// <param name="remotehost"></param>
        /// <returns></returns>
        private string GetInstanceNameForProcessId(string processName, int processId, string remotehost)
        {
            string result = "";
            try
            {
                // Create the appropriate PerformanceCounterCategory object.
                PerformanceCounterCategory cat = new PerformanceCounterCategory("Process", remotehost);
                // Get the instances associated with this category.
                string[] instances = cat.GetInstanceNames()
                    .Where(inst => inst.StartsWith(processName))
                    .ToArray();

                foreach (string instance in instances)
                {
                    // CategoryName, InstanceCounterName, instance, remotehost
                    using (PerformanceCounter cnt = new PerformanceCounter("Process", "ID Process", instance, remotehost))
                    {
                        int val = (int)cnt.RawValue;
                        if (val == processId)
                        {
                            return instance;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                result = "Exception occured in an attempt to get the performce data of the instance for the " + processId + ": " + e.Message;
            }
            return null;
        }

        /// <summary>
        /// Get the number of logical processors from the remote host.
        /// </summary>
        /// <param name="remotehost"></param>
        /// <returns></returns>
        private int GetLogicalProcessors(string remotehost)
        {
            ConnectionOptions connOptions = new ConnectionOptions();
            connOptions.Impersonation = ImpersonationLevel.Impersonate;
            connOptions.EnablePrivileges = true;
            ManagementScope manScope = new ManagementScope(String.Format(@"\\{0}\ROOT\CIMV2", remotehost), connOptions);
            manScope.Connect();

            // Query the Win32_Processor for NumberOfLogicalProcessors
            SelectQuery query = new SelectQuery("Select NumberOfLogicalProcessors from Win32_Processor");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(manScope, query);
            ManagementObjectCollection processorInformation = searcher.Get();

            int result = 0;

            foreach (ManagementObject obj in processorInformation)
            {
                // Retrieve the number of logical processors
                if (obj["NumberOfLogicalProcessors"] != null)
                {
                    try
                    {
                        int.TryParse(obj["NumberOfLogicalProcessors"].ToString(), out result);
                    }
                    catch
                    {
                        //
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// This method terminates the processes.
        /// </summary>
        /// <param name="remotehost"></param>
        /// <param name="processId"></param>
        /// <param name="username"></param>
        /// <returns></returns>
        private string TerminateRemoteProcess(string remotehost, int processId, string username)
        {
            string result = "Process ID " + processId + " not found";
            try
            {
                ConnectionOptions connOptions = new ConnectionOptions();
                connOptions.Impersonation = ImpersonationLevel.Impersonate;
                connOptions.EnablePrivileges = true;
                ManagementScope manScope = new ManagementScope(String.Format(@"\\{0}\ROOT\CIMV2", remotehost), connOptions);
                manScope.Connect();

                // Query the Win32_Process
                SelectQuery query = new SelectQuery("Select * from Win32_Process Where ProcessId = " + processId);
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(manScope, query);
                ManagementObjectCollection processList = searcher.Get();
                foreach (ManagementObject proc in processList)
                {
                    string procOwner = "None";
                    string[] Args = new string[] { "", "" };
                    int ReturnCode = Convert.ToInt32(proc.InvokeMethod("GetOwner", Args));
                    switch (ReturnCode)
                    {
                        case 0:
                            procOwner = Args[1] + "\\" + Args[0];
                            break;

                        default:
                            procOwner = "None";
                            break;

                    }
                    if (procOwner.ToUpper() == username.ToUpper())
                    {
                        // Get an input parameters object for this method
                        var inParams = proc.GetMethodParameters("Terminate");

                        // Fill in input parameter values
                        inParams["Reason"] = 0;

                        // Execute the method

                        var outParams = proc.InvokeMethod("Terminate", inParams, null);

                        // Detect success or failure based on return value
                        int.TryParse(outParams["ReturnValue"].ToString(), out int returnvalue);
                        switch (returnvalue)
                        {
                            case 0:
                                result = "Successful completion";
                                break;
                            case 2:
                                result = "Access denied";
                                break;
                            case 3:
                                result = "Insufficient privilege";
                                break;
                            case 8:
                                result = "Unknown failure";
                                break;
                            case 9:
                                result = "Path not found";
                                break;
                            case 21:
                                result = "Invalid parameter";
                                break;
                            default:
                                result = "Terminate failed with error code " + outParams["ReturnValue"].ToString();
                                break;
                        }
                    }
                    else
                    {
                        result = "Cannot terminate this process because " + username + " is not the process owner";
                    }
                }
                manScope = null;
                connOptions = null;
            }
            catch (ManagementException e)
            {
                result = "Exception occured in an attempt to terminate Process ID " + processId + ": " + e.Message;
            }
            return result;
        }

        // GET: api/UserProcess/Get?remotehost=
        /// <summary>
        /// This method gets the user processes based on the machine name passed in the URI.
        /// </summary>
        /// <param name="remotehost"></param>
        /// <returns>List of processes</returns>
        [Route("Get")]
        [HttpGet]
        public IEnumerable<UserProcess> GetRemoteProcessesByUsername(string remotehost)
        {
            bool EnableThisMethod = false;
            var pair = GetConfigurationSettings().FirstOrDefault(x => x.key == "EnableGetTerminateProcesses");
            if (pair != null)
            {
                Boolean.TryParse(pair.value.ToString(), out EnableThisMethod);
            }
            var result = new List<UserProcess> { };
            if (EnableThisMethod)
            {
                string username = GetLoggedOnUser();
                // return the output of he function, which is the UserProcess list
                result = GetRemoteUserProcesses(remotehost, username);
                Log.Information("Getting proccess from remote host " + remotehost + " for " + username + ".");
            }
            else
            {
                result.Add(new UserProcess
                {
                    Name = "The Get processes method is disabled.",
                    Description = "The EnableGetTerminateProcesses appSetting is set to False in the Web.config."
                });
                Log.Debug("The Get processes method is disabled.");
                Log.Debug("The EnableGetTerminateProcesses appSetting is set to False in the Web.config.");
            }
            return result;
        }

        // DELETE: api/UserProcess/Terminate
        /// <summary>
        /// This method terminates processes based on the machine name and pids passed in the body.
        /// Passing it in the body allows for multiple processes to be passed in an array without overcomplicating the URI.
        /// </summary>
        /// <param name="terminateprocess"></param>
        /// <returns></returns>
        [Route("Terminate")]
        [HttpDelete]
        public IHttpActionResult TerminateRemoteProcessByID([FromBody]TerminateProcess terminateprocess)
        {
            StringBuilder stringBuilder = new StringBuilder();
            bool EnableThisMethod = false;
            var pair = GetConfigurationSettings().FirstOrDefault(x => x.key == "EnableGetTerminateProcesses");
            if (pair != null)
            {
                Boolean.TryParse(pair.value.ToString(), out EnableThisMethod);
            }
            if (EnableThisMethod)
            {
                string username = GetLoggedOnUser();
                string remotehost = terminateprocess.RemoteHost;
                foreach (string pid in terminateprocess.PID)
                {
                    int.TryParse(pid, out int intpid);
                    string result = TerminateRemoteProcess(remotehost, intpid, username);
                    stringBuilder.AppendLine("PID " + pid + " termination result:" + result);
                    Log.Information("Terminating PID " + pid + " from remote host " + remotehost + " for " + username + ". Result: " + result);
                }
            }
            else
            {
                stringBuilder.AppendLine("The Terminate processes method is disabled.");
                Log.Debug("The Terminate processes method is disabled.");
                stringBuilder.AppendLine("The EnableGetTerminateProcesses appSetting is set to False in the Web.config.");
                Log.Debug("The EnableGetTerminateProcesses appSetting is set to False in the Web.config.");
            }
            Log.Debug(stringBuilder.ToString().Substring(0, stringBuilder.ToString().Length - 1));
            return Ok(stringBuilder.ToString());
        }

    }
}
