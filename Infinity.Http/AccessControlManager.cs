using Infinity.Core;

namespace Infinity.Http
{
    /// <summary>
    /// Access control manager.  Dictates which connections are permitted or denied.
    /// </summary>
    public class AccessControlManager
    {
        /// <summary>
        /// Matcher to match denied addresses.
        /// </summary>
        public IpMatcher DenyList
        {
            get
            {
                return deny_list;
            }
            set
            {
                if (value == null)
                {
                    value = new IpMatcher();
                }

                deny_list = value;
            }
        }

        /// <summary>
        /// Matcher to match permitted addresses.
        /// </summary>
        public IpMatcher PermitList
        {
            get
            {
                return permit_list;
            }
            set
            {
                if (value == null)
                {
                    value = new IpMatcher();
                }

                permit_list = value;
            }
        }

        /// <summary>
        /// Access control mode, either DefaultPermit or DefaultDeny.
        /// DefaultPermit: allow everything, except for those explicitly denied.
        /// DefaultDeny: deny everything, except for those explicitly permitted.
        /// </summary>
        public AccessControlMode Mode { get; set; } = AccessControlMode.DefaultPermit;

        private IpMatcher deny_list = new IpMatcher();
        private IpMatcher permit_list = new IpMatcher();

        /// <summary>
        /// Instantiate.
        /// </summary> 
        /// <param name="mode">Access control mode.</param>
        public AccessControlManager(AccessControlMode _mode = AccessControlMode.DefaultPermit)
        {
            Mode = _mode;
        }

        /// <summary>
        /// Permit or deny a request based on IP address.  
        /// When operating in 'default deny', only specified entries are permitted. 
        /// When operating in 'default permit', everything is allowed unless explicitly denied.
        /// </summary>
        /// <param name="_ip">The IP address to evaluate.</param>
        /// <returns>True if permitted.</returns>
        public bool Permit(string _ip)
        {
            if (string.IsNullOrEmpty(_ip))
            {
                throw new ArgumentNullException(nameof(_ip));
            }

            switch (Mode)
            {
                case AccessControlMode.DefaultDeny:
                    {
                        return PermitList.MatchExists(_ip);
                    }

                case AccessControlMode.DefaultPermit:
                    {
                        if (DenyList.MatchExists(_ip))
                        {
                            return false;
                        }

                        return true;
                    }

                default:
                    {
                        throw new ArgumentException("Unknown access control mode: " + Mode.ToString());
                    }
            }
        }
    }
}
