﻿// Version 1.6
// Written by Jeremy Saunders (jeremy@jhouseconsulting.com) 13th June 2020
// Modified by Jeremy Saunders (jeremy@jhouseconsulting.com) 12th October 2021
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
// Required for MailKit
using MailKit.Net.Smtp;
using MailKit;
using MimeKit;
using MailKit.Security;

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
        /// Get the configuration information from appSettings in the Web.config.
        /// Only include the settings that start with sssrt.
        /// </summary>
        private List<ConfigSettings> GetConfigurationSettings()
        {
            List<ConfigSettings> ConfigurationSettings = new List<ConfigSettings> { };
            NameValueCollection appSettings = ConfigurationManager.AppSettings;

            foreach (string s in appSettings.AllKeys)
            {
                if (s.IndexOf("sssrt", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    ConfigurationSettings.Add(new ConfigSettings
                    {
                        key = s.Split(':')[1],
                        value = appSettings.Get(s)
                    });
                }
            }
            return ConfigurationSettings;
        }

        /// <summary>
        /// Send SMTP Email using MailKit.
        /// - It gets the congiguration from the Web.Config.
        /// - It receives the subject, body and username as parameters. The username is simply used for the logging output returned.
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="body"></param>
        /// <param name="username"></param>
        /// <returns></returns>
        private string SendMail(string subject, string body, string username)
        {
            StringBuilder stringBuilder = new StringBuilder();
            string SenderName = string.Empty;
            string SenderEmailAddress = string.Empty;
            string RecipientEmailAddresses = string.Empty;
            string SmtpServer = string.Empty;
            int SmtpPort = 25;
            string SmtpAuthUsername = string.Empty;
            string SmtpAuthPassword = string.Empty;
            string SubjectStartsWith = string.Empty;
            string BodyTextStartsWith = string.Empty;
            foreach (var pair in GetConfigurationSettings())
            {
                if (pair.key.IndexOf("SenderName", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    SenderName = pair.value.ToString();
                }
                if (pair.key.IndexOf("SenderEmailAddress", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    SenderEmailAddress = pair.value.ToString();
                }
                if (pair.key.IndexOf("RecipientEmailAddresses", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    RecipientEmailAddresses = pair.value.ToString();
                }
                if (pair.key.IndexOf("SmtpServer", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    SmtpServer = pair.value.ToString();
                }
                if (pair.key.IndexOf("SmtpPort", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    int.TryParse(pair.value.ToString(), out SmtpPort);
                }
                if (pair.key.IndexOf("SmtpAuthUsername", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    SmtpAuthUsername = pair.value.ToString();
                }
                if (pair.key.IndexOf("SmtpAuthPassword", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    SmtpAuthPassword = pair.value.ToString();
                }
                if (pair.key.IndexOf("SubjectStartsWith", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    SubjectStartsWith = pair.value.ToString();
                }
                if (pair.key.IndexOf("BodyTextStartsWith", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    BodyTextStartsWith = pair.value.ToString();
                }
            }

            MimeMessage message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmailAddress));

            string[] ToMuliId = RecipientEmailAddresses.Split(',');
            foreach (string ToEMailId in ToMuliId)
            {
                message.To.Add(MailboxAddress.Parse(ToEMailId));
            }

            if (!string.IsNullOrEmpty(SubjectStartsWith))
            {
                subject = SubjectStartsWith + " " + subject;
            }
            message.Subject = subject;

            if (!string.IsNullOrEmpty(BodyTextStartsWith))
            {
                body = BodyTextStartsWith + " " + body;
            }
            message.Body = new TextPart("plain")
            {
                Text = @body
            };

            SmtpClient client = new SmtpClient();
            try
            {
                client.Connect(SmtpServer, SmtpPort, SecureSocketOptions.Auto);
                // Note: since we don't have an OAuth2 token, disable
                // the XOAUTH2 authentication mechanism.
                client.AuthenticationMechanisms.Remove("XOAUTH2");
                if (!string.IsNullOrEmpty(SmtpAuthUsername) && !string.IsNullOrEmpty(SmtpAuthPassword))
                {
                    client.Authenticate(SmtpAuthUsername, SmtpAuthPassword);
                }
                client.Send(message);
                stringBuilder.AppendLine("Email successfully sent on behalf of " + username + ".");
            }
            catch (Exception ex)
            {
                stringBuilder.AppendLine("Email failed to send on behalf of " + username + ": " + ex.Message);
            }
            finally
            {
                client.Disconnect(true);
                client.Dispose();
            }
            return stringBuilder.ToString();
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
            if (!string.IsNullOrEmpty(result))
            {
                Log.Debug(result);
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
            string result = string.Empty;
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
            if (!string.IsNullOrEmpty(result))
            {
                Log.Debug(result);
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
            string result = string.Empty;
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
            if (!string.IsNullOrEmpty(result))
            {
                Log.Debug(result);
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
            string result = string.Empty;
            int processorCount = 0;
            try
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

                foreach (ManagementObject obj in processorInformation)
                {
                    // Retrieve the number of logical processors
                    if (obj["NumberOfLogicalProcessors"] != null)
                    {
                        try
                        {
                            int.TryParse(obj["NumberOfLogicalProcessors"].ToString(), out processorCount);
                            result = "The number of logical processors on " + remotehost + " is " + processorCount;
                        }
                        catch
                        {
                            result = "Unable to retrieve the number of logical processors on " + remotehost;
                        }
                    }
                    else
                    {
                        result = "Unable to find the number of logical processors on " + remotehost;
                    }
                }
            }
            catch (Exception e)
            {
                result = "Exception occured in an attempt to get the number of logical processors on " + remotehost + ": " + e.Message;
            }
            if (!string.IsNullOrEmpty(result))
            {
                Log.Debug(result);
            }
            return processorCount;
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
                    string procName = proc["Name"].ToString();
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
                                result = "Successful termination of the " + procName + " process.";
                                break;
                            case 2:
                                result = "Access denied terminating the " + procName + " process.";
                                break;
                            case 3:
                                result = "Insufficient privileges to terminate the " + procName + " process.";
                                break;
                            case 8:
                                result = "Unknown failure terminating the " + procName + " process.";
                                break;
                            case 9:
                                result = "Path not found to the " + procName + " process.";
                                break;
                            case 21:
                                result = "Invalid parameter supplied to terminate the " + procName + " process.";
                                break;
                            default:
                                result = "Attempt to terminate the " + procName + " process failed with error code " + outParams["ReturnValue"].ToString() + ".";
                                break;
                        }
                    }
                    else
                    {
                        result = "Cannot terminate the " + procName + " process with a Process ID of " + processId + " because " + username + " is not the process owner.";
                    }
                }
                manScope = null;
                connOptions = null;
            }
            catch (ManagementException e)
            {
                result = "Exception occured in an attempt to terminate Process ID " + processId + " for " + username + ": " + e.Message;
            }
            Log.Debug(result);
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
                bool SendEmail = false;
                pair = GetConfigurationSettings().FirstOrDefault(x => x.key == "EnableEmailForTerminateProcesses");
                if (pair != null)
                {
                    Boolean.TryParse(pair.value.ToString(), out SendEmail);
                }
                string message = string.Empty;
                string username = GetLoggedOnUser();
                string remotehost = terminateprocess.RemoteHost;
                foreach (string pid in terminateprocess.PID)
                {
                    int.TryParse(pid, out int intpid);
                    string result = TerminateRemoteProcess(remotehost, intpid, username);
                    stringBuilder.AppendLine("PID " + pid + " termination result:" + result);
                    string localmessage = "Terminating PID " + pid + " from remote host " + remotehost + " for " + username + ". Result: " + result;
                    Log.Information(localmessage);
                    if (!string.IsNullOrEmpty(message))
                    {
                        message = message + Environment.NewLine + localmessage;
                    }
                    else
                    {
                        message = localmessage;
                    }
                }
                if (SendEmail && terminateprocess.PID.Count() > 0)
                {
                    if (terminateprocess.PID.Count() == 1)
                    {
                        SendMail("Terminate process for " + username, message, username);
                    }
                    else
                    {
                        SendMail("Terminate processes for " + username, message, username);
                    }
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
