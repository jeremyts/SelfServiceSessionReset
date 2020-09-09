// Version 1.
// Written by Jeremy Saunders (jeremy@jhouseconsulting.com) 13th June 2020
// Modified by Jeremy Saunders (jeremy@jhouseconsulting.com) 9th September 2020
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SelfServiceSessionReset.Models
{
    public class CtxSession
    {
        public string MachineName { get; set; }
        public string HostedMachineName { get; set; }
        public string SessionState { get; set; }
        public string StartTime { get; set; }
        public string SessionStateChangeTime { get; set; }
        public string OSType { get; set; }
        public string SessionSupport { get; set; }
        public string SessionId { get; set; }
        public string ApplicationState { get; set; }
        public string ApplicationStateLastChangeTime { get; set; }
        public string[] ApplicationsInUse { get; set; }
        public string IdleDuration { get; set; }
        public string IdleSince { get; set; }
        public string RestartSupported { get; set; }
        public string Hidden { get; set; }
        public string PowerState { get; set; }
        public string RegistrationState { get; set; }
        public string MaintenanceMode { get; set; }
        public bool Include { get; set; }

        public CtxSession()
        {
            MachineName = string.Empty;
            HostedMachineName = string.Empty;
            SessionState = string.Empty;
            StartTime = string.Empty;
            SessionStateChangeTime = string.Empty;
            OSType = string.Empty;
            SessionSupport = string.Empty;
            SessionId = string.Empty;
            ApplicationState = string.Empty;
            ApplicationStateLastChangeTime = string.Empty;
            //string[] ApplicationsInUse = new string[] { };
            IdleDuration = string.Empty;
            IdleSince = string.Empty;
            RestartSupported = string.Empty;
            Hidden = string.Empty;
            PowerState = string.Empty;
            RegistrationState = string.Empty;
            MaintenanceMode = string.Empty;
            Include = false;
        }
    }
    public class CtxSessionsToAction
    {
        public string SiteName { get; set; }
        public string DeliveryControllers { get; set; }
        public string Port { get; set; }
        public string UserName { get; set; }
        public string[] MachineNames { get; set; }
        public bool Reset { get; set; }
        public bool Hide { get; set; }
        public CtxSessionsToAction()
        {
            SiteName = string.Empty;
            DeliveryControllers = string.Empty;
            Port = string.Empty;
            UserName = string.Empty;
            //string[] MachineNames = new string[] { };
            Reset = false;
            Hide = false;
        }

    }
    public class CtxSites
    {
        public string FriendlyName { get; set; }
        public string Name { get; set; }
        public string DeliveryControllers { get; set; }
        public string Port { get; set; }
        public string Default { get; set; }
        public CtxSites()
        {
            FriendlyName = string.Empty;
            Name = string.Empty;
            DeliveryControllers = string.Empty;
            Port = string.Empty;
            Default = string.Empty;
        }
    }
}