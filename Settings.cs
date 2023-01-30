using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoSync
{
    [Serializable]
    public class Settings
    {
        public bool Relinquish { get; set; } = true;
        public bool Sync { get; set; } = true;
        public int RelinquishCheck { get; set; } = 15;
        public int SyncCheck { get; set; } = 120;
    }
}
