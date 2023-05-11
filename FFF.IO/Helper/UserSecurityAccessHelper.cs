using System.Security.AccessControl;
using System.Security.Principal;

namespace FFF.IO.Helper
{
    internal static class UserSecurityAccessHelper
    {
        public static bool HasFileOrDirectoryAccess(FileSystemRights right, AuthorizationRuleCollection acl)
        {
            WindowsIdentity currentUser = WindowsIdentity.GetCurrent();
            WindowsPrincipal currentPrincipal = new WindowsPrincipal(currentUser);

            bool allow = false;
            bool inheritedAllow = false;
            bool inheritedDeny = false;

            for (int i = 0; i < acl.Count; i++)
            {
                FileSystemAccessRule currentRule = (FileSystemAccessRule)acl[i];
                // If the current rule applies to the current user.
                if (currentUser.User.Equals(currentRule.IdentityReference) ||
                    currentPrincipal.IsInRole((SecurityIdentifier)currentRule.IdentityReference))
                {
                    if (currentRule.AccessControlType.Equals(AccessControlType.Deny))
                    {
                        if ((currentRule.FileSystemRights & right) == right)
                        {
                            if (currentRule.IsInherited) inheritedDeny = true;
                            // Non inherited "deny" takes overall precedence.
                            else return false;
                        }
                    }
                    else if (currentRule.AccessControlType.Equals(AccessControlType.Allow))
                    {
                        if ((currentRule.FileSystemRights & right) == right)
                        {
                            if (currentRule.IsInherited) inheritedAllow = true;
                            else allow = true;
                        }
                    }
                }
            }
            // Non inherited "allow" takes precedence over inherited rules.
            if (allow) return true;

            return inheritedAllow && !inheritedDeny;
        }
    }
}
