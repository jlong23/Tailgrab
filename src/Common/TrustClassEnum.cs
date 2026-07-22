using System;
using System.Collections.Generic;
using System.Text;

namespace Tailgrab.Common
{
    public enum TrustClassEnum { 
        VISITOR = 0,
        NEW_USER = 1,
        USER = 2,
        KNOWN_USER = 3,
        TRUSTED_USER = 4,
        PROBABLE_TROLL = 5,
        NUISANCE = 6,
    }
}
