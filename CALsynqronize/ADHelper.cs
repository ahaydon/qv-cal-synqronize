using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Text;

using System.DirectoryServices;

namespace CALsynqronize
{
    // This class originally came from...
    // http://www.codeproject.com/Articles/27281/Retrieve-Names-from-Nested-AD-Groups
    
    class ADHelper
    {
        /// <span class="code-SummaryComment"><summary></span>
        /// searchedGroups will contain all groups already searched, in order to
        /// prevent endless loops when there are circular structures in the groups.
        /// <span class="code-SummaryComment"></summary></span>
        static Hashtable searchedGroups = null;

        /// <span class="code-SummaryComment"><summary></span>
        /// x will return all users in the group passed in as a parameter
        /// the names returned are the SAM Account Name of the users.
        /// The function will recursively search all nested groups.
        /// Remark: if there are multiple groups with the same name, 
        /// this function will just
        /// use the first one it finds.
        /// <span class="code-SummaryComment"></summary></span>
        /// <span class="code-SummaryComment"><param name="strGroupName">Name of the group, </span>
        /// which the users should be retrieved from<span class="code-SummaryComment"></param></span>
        /// <span class="code-SummaryComment"><returns>ArrayList containing the SAM Account Names </span>
        /// of all users in this group and any nested groups<span class="code-SummaryComment"></returns></span>
        static public ArrayList GetGroupMembers(string strGroupName)
        {
            ArrayList groupMembers = new ArrayList();
            searchedGroups = new Hashtable();

            // find group
            DirectorySearcher search = new DirectorySearcher();

            search.SizeLimit = 0;
            search.PageSize = 1000;
            
            search.Filter = String.Format("(&(objectCategory=group)(cn={0}))", strGroupName);
            search.PropertiesToLoad.Add("distinguishedName");
            SearchResult sru = null;
            DirectoryEntry group;

            try
            {
                sru = search.FindOne();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            group = sru.GetDirectoryEntry();

            groupMembers = GetUsersInGroup(group.Properties["distinguishedName"].Value.ToString());

            return groupMembers;
        }

        /// <span class="code-SummaryComment"><summary></span>
        /// getUsersInGroup will return all users in the group passed in as a parameter
        /// the names returned are the SAM Account Name of the users.
        /// The function will recursively search all nested groups.
        /// <span class="code-SummaryComment"></summary></span>
        /// <span class="code-SummaryComment"><param name="strGroupDN">DN of the group, </span>
        /// which the users should be retrieved from<span class="code-SummaryComment"></param></span>
        /// <span class="code-SummaryComment"><returns>ArrayList containing the SAM Account Names </span>
        /// of all users in this group and any nested groups<span class="code-SummaryComment"></returns></span>
        private static ArrayList GetUsersInGroup(string strGroupDN)
        {
            ArrayList groupMembers = new ArrayList();
            searchedGroups.Add(strGroupDN, strGroupDN);

            // find all users in this group
            DirectorySearcher ds = new DirectorySearcher();
            ds.Filter = String.Format("(&(memberOf={0})(objectClass=person))", strGroupDN);

            ds.SizeLimit = 0;
            ds.PageSize = 1000;

            ds.PropertiesToLoad.Add("distinguishedName");
            ds.PropertiesToLoad.Add("samaccountname");

            foreach (SearchResult sr in ds.FindAll())
            {
                if (sr.Properties.Count>0)
                    groupMembers.Add(sr.Properties["samaccountname"][0].ToString().ToUpper());
            }

            // get nested groups
            ArrayList al = GetNestedGroups(strGroupDN);
            foreach (object g in al)
            {
                // only if we haven't searched this group before - avoid endless loops
                if (!searchedGroups.ContainsKey(g))
                {
                    // get members in nested group
                    ArrayList ml = GetUsersInGroup(g as string);
                    // add them to result list
                    foreach (object s in ml)
                    {
                        groupMembers.Add(s as string);
                    }
                }
            }

            return groupMembers;
        }

        /// <span class="code-SummaryComment"><summary></span>
        /// getNestedGroups will return an array with the DNs of all groups contained
        /// in the group that was passed in as a parameter
        /// <span class="code-SummaryComment"></summary></span>
        /// <span class="code-SummaryComment"><param name="strGroupDN">DN of the group, </span>
        /// which the nested groups should be retrieved from<span class="code-SummaryComment"></param></span>
        /// <span class="code-SummaryComment"><returns>ArrayList containing the DNs of each group </span>
        /// contained in the group passed in as a parameter<span class="code-SummaryComment"></returns></span>
        private static ArrayList GetNestedGroups(string strGroupDN)
        {
            ArrayList groupMembers = new ArrayList();

            // find all nested groups in this group
            DirectorySearcher ds = new DirectorySearcher();
            ds.Filter = String.Format("(&(memberOf={0})(objectClass=group))", strGroupDN);

            ds.SizeLimit = 0;
            ds.PageSize = 1000;

            ds.PropertiesToLoad.Add("distinguishedName");

            foreach (SearchResult sr in ds.FindAll())
            {
                groupMembers.Add(sr.Properties["distinguishedName"][0].ToString());
            }

            return groupMembers;
        }
    }
}