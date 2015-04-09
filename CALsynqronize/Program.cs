using System;
using System.Collections.Generic;
using System.Linq;
using CALsynqronize.QMSAPI;
using NDesk.Options;
using LogLevel = NLog.LogLevel;
using Exception = System.Exception;

namespace CALsynqronize
{
    public class Parameters
    {
        public string GroupName { get; set; }
        public string Prefix { get; set; }
        public string QlikViewServer { get; set; }
    }

    class Program
    {
        static readonly LogProperties LogProperties = new LogProperties { };

        static Parameters parameters = new Parameters{GroupName = "", Prefix = "", QlikViewServer = ""};

        static QMSClient apiClient = new QMSClient();

        static void Main(string[] args)
        {

            bool help = false;
            bool version = false;
            bool remove = false;

            var p = new OptionSet()
                            {
                                {"g|group=", "Comma separated {list} of AD groups to synqronize", v => parameters.GroupName = v},
                                {"p|prefix:", "{Domain} to add before sAMAccountName", v => parameters.Prefix = v},
                                {"s|server:", "{Name} of QlikView Server to synqronize", v => parameters.QlikViewServer = v},
                                {"r|remove", "Remove ALL Named CAL's from QlikView Server", v => remove = v != null},
                                {"V|version", "Show version information", v => version = v != null},
                                {"?|h|help", "Show usage information", v => help = v != null}
                            };

            p.Parse(args);

            if (help || args.Length == 0)
            {
                ShowHelp(p);
                return;
            }

            if (version)
            {
                Console.WriteLine("CALsynqronize version 20150316\n");
                Console.WriteLine("This program comes with ABSOLUTELY NO WARRANTY.");
                Console.WriteLine("This is free software, and you are welcome to redistribute it");
                Console.WriteLine("under certain conditions.");
                return;
            }

            if(remove)
            {

                try
                {
                    // read cal information from qlikview server
                    var namedCalsOnServer = GetUsersFromServer();

                    // remove all users
                    RemoveCALs(namedCalsOnServer);

                    return;
                }
                catch (Exception ex)
                {
                    LogHelper.Log(LogLevel.Error, ex.Message, LogProperties);
                    return;
                }
            }

            try
            {
                if (String.IsNullOrEmpty(parameters.GroupName))
                {
                    LogHelper.Log(LogLevel.Error, "--group parameter is required!", LogProperties);
                    return;
                }

                // read members from active directory group
                var usersFromActiveDirectory = GetUsersFromAd();

                if(usersFromActiveDirectory.Count > 0)
                {
                    // read cal information from qlikview server
                    var namedCalsOnServer = GetUsersFromServer();

                    // get list of users who has a license but is not present in the AD group anymore
                    var removeUsers = namedCalsOnServer.Except(usersFromActiveDirectory).Distinct().ToList();

                    // get list of new users to add who not already has a license
                    Console.WriteLine("Removing duplicates and existing identities...");
                    var allocateUsers = usersFromActiveDirectory.Except(namedCalsOnServer).Distinct().ToList();

                    Console.WriteLine("Number of CAL's to remove: " + removeUsers.Count + ", number of CAL's to add: " + allocateUsers.Count);

                    SynqronizeCALs(removeUsers, allocateUsers);
                }
                else
                {
                    LogHelper.Log(LogLevel.Error, "No members were found in group '" + parameters.GroupName + "'", LogProperties);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                LogHelper.Log(LogLevel.Error, ex.Message, LogProperties);
            }
        }

        static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: CALsynqronize [options]");
            Console.WriteLine("Synqronizes a specified Active Directory group with QlikView Named CAL's.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
            Console.WriteLine();
            Console.WriteLine("Options can be in the form -option, /option or --long-option");
        }

        private static List<string> GetUsersFromAd()
        {
            var groups = parameters.GroupName.Split(',');

            var l = new List<string>();

            foreach (var _grp in groups)
            {
                var previousCount = l.Count();

                Console.Write("Reading members from AD group '" + _grp + "'...");

                var u = ADHelper.GetGroupMembers(_grp);

                l.AddRange(u.Cast<string>().ToList());
                
                Console.WriteLine(" (Found " + (l.Count() - previousCount) + ")");
            }

            if (!String.IsNullOrEmpty(parameters.Prefix))
            {
                Console.WriteLine("Adding prefix '" + parameters.Prefix + "' to all identities...");

                for (int i = 0; i < l.Count; i++)
                {
                    l[i] = parameters.Prefix.ToUpper() + "\\" + l[i];
                }
            }

            return l;
        }

        public static ServiceInfo GetQlikViewServer()
        {
            // Get a time limited service key
            ServiceKeyClientMessageInspector.ServiceKey = apiClient.GetTimeLimitedServiceKey();

            // get a list of QVS services
            List<ServiceInfo> qvsServices = apiClient.GetServices(ServiceTypes.QlikViewServer);

            int serverId = 0;

            for (int i = 0; i < qvsServices.Count; i++)
            {
                if (qvsServices[i].Name.ToUpper() == parameters.QlikViewServer.ToUpper())
                {
                    serverId = i;
                    break;
                }
            }

            return qvsServices[serverId];
        }

        public static List<string> GetUsersFromServer()
        {
            // Get a time limited service key
            ServiceKeyClientMessageInspector.ServiceKey = apiClient.GetTimeLimitedServiceKey();

            ServiceInfo qvs = GetQlikViewServer();

            Console.Write("Reading CAL information from {0}...", qvs.Name);

            var currentCals = new List<string>();

            CALConfiguration config = apiClient.GetCALConfiguration(qvs.ID, CALConfigurationScope.NamedCALs);

            // Get Named CALs
            currentCals.AddRange(config.NamedCALs.AssignedCALs.Select(c => c.UserName.ToUpper()));

            Console.WriteLine(" (Found " + currentCals.Count + ")");

            return currentCals;
        }

        private static void SynqronizeCALs(List<String> removeCALs, List<String> allocateCALs)
        {
            if (removeCALs.Count + allocateCALs.Count <= 0) return;

            Console.WriteLine("Synqronizing...");

            // Get a time limited service key
            ServiceKeyClientMessageInspector.ServiceKey = apiClient.GetTimeLimitedServiceKey();

            ServiceInfo qvs = GetQlikViewServer();

            CALConfiguration config = apiClient.GetCALConfiguration(qvs.ID, CALConfigurationScope.NamedCALs);

            // Get number of users BEFORE modifications
            var namedCALsAssigned = config.NamedCALs.AssignedCALs.Count;
            var inLicense = config.NamedCALs.InLicense;
            var namedCALsLimit = config.NamedCALs.Limit;

            // Check if there's enough available licenses
            if ((allocateCALs.Count - removeCALs.Count) <= (inLicense - namedCALsAssigned))
            {
                // convert list<string> to list<AssignedNameCAL>
                var r = removeCALs.Select(cal => new AssignedNamedCAL {UserName = cal}).ToList();

                // Remove named CALs
                foreach (
                    AssignedNamedCAL c in
                        config.NamedCALs.AssignedCALs.ToList().SelectMany(
                            cals => r.Where(item => item.UserName == cals.UserName).Select(item => cals)))
                {
                    LogHelper.Log(LogLevel.Debug, "REMOVE: " + c.UserName, LogProperties);
                    config.NamedCALs.AssignedCALs.Remove(c);
                }

                // Add named CALs
                foreach (var cal in allocateCALs)
                {
                    LogHelper.Log(LogLevel.Debug, "ADD: " + cal, LogProperties);
                    config.NamedCALs.AssignedCALs.Add(new AssignedNamedCAL {UserName = cal});
                }

                // save changes
                apiClient.SaveCALConfiguration(config);

                // Get number of users AFTER modifications
                namedCALsAssigned = config.NamedCALs.AssignedCALs.Count;
                inLicense = config.NamedCALs.InLicense;
                namedCALsLimit = config.NamedCALs.Limit;

                LogHelper.Log(LogLevel.Debug, "ASSIGNED: " + namedCALsAssigned, LogProperties);
                LogHelper.Log(LogLevel.Debug, "IN LICENSE: " + inLicense, LogProperties);
                if (inLicense != namedCALsLimit)
                    LogHelper.Log(LogLevel.Debug, "LIMIT: " + namedCALsLimit, LogProperties);
                LogHelper.Log(LogLevel.Debug, "AVAILABLE: " + (inLicense - namedCALsAssigned), LogProperties);

                Console.WriteLine("Done!");
            }
            else
            {
                LogHelper.Log(LogLevel.Error, "Not enough CAL's available on server", LogProperties);
            }
        }

        private static void RemoveCALs(List<String> removeCALs)
        {
            Console.WriteLine("Removing ALL Named CAL's...");

            // Get a time limited service key
            ServiceKeyClientMessageInspector.ServiceKey = apiClient.GetTimeLimitedServiceKey();

            ServiceInfo qvs = GetQlikViewServer();

            CALConfiguration config = apiClient.GetCALConfiguration(qvs.ID, CALConfigurationScope.NamedCALs);

            // convert list<string> to list<AssignedNameCAL>
            var r = removeCALs.Select(cal => new AssignedNamedCAL { UserName = cal }).ToList();

            // Remove named CALs
            foreach (
                AssignedNamedCAL c in
                    config.NamedCALs.AssignedCALs.ToList().SelectMany(
                        cals => r.Where(item => item.UserName == cals.UserName).Select(item => cals)))
            {
                LogHelper.Log(LogLevel.Debug, "REMOVE: " + c.UserName, LogProperties);
                config.NamedCALs.AssignedCALs.Remove(c);
            }

            // save changes
            apiClient.SaveCALConfiguration(config);

            Console.WriteLine("Done!");
        }
    }
}
