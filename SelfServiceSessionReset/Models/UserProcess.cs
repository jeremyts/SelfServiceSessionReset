// Version 1.0
// Written by Jeremy Saunders (jeremy@jhouseconsulting.com) 13th June 2020
// Modified by Jeremy Saunders (jeremy@jhouseconsulting.com) 2nd August 2020
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SelfServiceSessionReset.Models
{
    public class UserProcess
    {
        public string Name { get; set; }
        public string PID { get; set; }
        public string BytesPrivateMemorySize { get; set; }
        public string BytesWorkingSet { get; set; }
        public string BytesPeakWorkingSet { get; set; }
        public string BytesPagedMemorySize { get; set; }
        public double CpuUsagePercentage { get; set; }
        public string HandleCount { get; set; }
        public string Threads { get; set; }
        public string CommandLine { get; set; }
        public string Description { get; set; }

        public UserProcess()
        {
            Name = string.Empty;
            PID = string.Empty;
            BytesPrivateMemorySize = string.Empty;
            BytesWorkingSet = string.Empty;
            BytesPeakWorkingSet = string.Empty;
            BytesPagedMemorySize = string.Empty;
            CpuUsagePercentage = 0;
            HandleCount = string.Empty;
            Threads = string.Empty;
            CommandLine = string.Empty;
            Description = string.Empty;
        }
    }
    public class TerminateProcess
    {
        public string RemoteHost { get; set; }
        public string[] PID { get; set; }

        public TerminateProcess()
        {
            RemoteHost = string.Empty;
            //string[] PID = new string[] { };
        }
    }

}