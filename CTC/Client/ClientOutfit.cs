using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CTC
{
    public class ClientOutfit
    {
        public int LookType;
        public int LookItem;
        public int LookHead;
        public int LookBody;
        public int LookLegs;
        public int LookFeet;
        // Phase 8: addons bitmask (0 = none, 1 = first addon, 2 = second addon, 3 = both).
        // Absent in 7.4 protocol; present in 8.6.
        public byte Addons;
    }
}
