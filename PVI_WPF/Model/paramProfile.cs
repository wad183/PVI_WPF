using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PVI_WPF
{
    internal class paramProfile
    {
        public string Name { get; set; }

        public DetectParams detectParams { get; set; } = new();
    }
}
